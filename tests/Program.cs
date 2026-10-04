using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Swyf.CustomAI;
using Installer = Swyf.CustomAI.Installation.Program;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
var output = Path.Combine(root, "artifacts", "tests", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
Directory.CreateDirectory(output);
int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
var backendConfig = Path.Combine(output, "customai.toml");
Check(!BackendConfig.Read(backendConfig), "missing Kolkata configuration keeps the backend enabled");
foreach (var text in new[] { "", "# installer setting\n", "disable_kolkata_api = false\n" })
{
    File.WriteAllText(backendConfig, text);
    Check(!BackendConfig.Read(backendConfig), "absent or false Kolkata setting keeps the backend enabled");
}
File.WriteAllText(backendConfig, "  # startup configuration\r\n  disable_kolkata_api  =  true  # disabled\r\n", new UTF8Encoding(true));
Check(BackendConfig.Read(backendConfig), "Kolkata configuration supports BOM, comments and whitespace");
foreach (var text in new[] { "disable_kolkata_api = maybe", "disable_kolkata_api = True", "disable_kolkata_api = \"true\"", "disable_kolkata_api = true\ndisable_kolkata_api = false", "[backend]\ndisable_kolkata_api = true", "unexpected = true" })
{
    File.WriteAllText(backendConfig, text);
    try { BackendConfig.Read(backendConfig); throw new Exception("Invalid Kolkata configuration was accepted: " + text); }
    catch (FormatException) { Check(true, "invalid or ambiguous Kolkata configuration is rejected"); }
}
async Task Failure(Func<Task> task, string code)
{ try { await task(); throw new Exception("Expected " + code); } catch (ApiFailure e) { Check(e.Code == code, code); } }
var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
var fake = builder.Build();
JsonObject? observed = null; string? observedAuth = null; int requests = 0;
var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
const string greeting = "{\"dialogue\":\"Hello, I am listening.\",\"trust_percent\":50,\"emotion\":\"NEUTRAL\"}";
JsonObject Response(string content) => new() { ["choices"] = new JsonArray(new JsonObject { ["message"] = new JsonObject { ["content"] = content } }) };
fake.MapPost("/{**path}", async (HttpContext ctx) =>
{
    requests++;
    observed = await ctx.Request.ReadFromJsonAsync<JsonObject>();
    observedAuth = ctx.Request.Headers.Authorization;
    var path = ctx.Request.Path.Value!;
    if (path.Contains("no-sampling"))
    {
        var rejected = observed!.ContainsKey("temperature") ? "temperature" : observed.ContainsKey("top_p") ? "top_p" : null;
        if (rejected != null)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { error = new { message = $"Unsupported parameter: '{rejected}' is not supported with this model." } });
            return;
        }
    }
    if (path.Contains("slow"))
    { try { await Task.Delay(30000, ctx.RequestAborted); } catch (OperationCanceledException) { cancelled.TrySetResult(); } return; }
    if (path.Contains("unauthorized")) { ctx.Response.StatusCode = 401; await ctx.Response.WriteAsync("secret-must-not-leak"); return; }
    if (path.Contains("missing")) { ctx.Response.StatusCode = 404; return; }
    if (path.Contains("rate")) { ctx.Response.StatusCode = 429; return; }
    if (path.Contains("redirect")) { ctx.Response.StatusCode = 307; ctx.Response.Headers.Location = "/v1/chat/completions"; return; }
    if (path.Contains("invalid")) { await ctx.Response.WriteAsync("broken"); return; }
    if (path.Contains("oversize")) { await ctx.Response.WriteAsync(new string('x', Provider.MaxResponse + 10)); return; }
    if (path.Contains("wrong-schema")) { await ctx.Response.WriteAsJsonAsync(Response("{\"dialogue\":\"hi\"}")); return; }
    await ctx.Response.WriteAsJsonAsync(Response(greeting));
});
await fake.StartAsync();
var url = fake.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var provider = new Provider();
var input = JsonNode.Parse("""{"model":"old","messages":[{"role":"system","content":"keep"},{"role":"user","content":"hello"}],"temperature":0.9,"max_tokens":128,"session_id":"private","provider":{"require_parameters":true},"response_format":{"type":"json_schema","json_schema":{"schema":{"type":"object"}}}}""")!.AsObject();
var settings = new Settings(true, url + "/v1/", "fake-model");
string State(Settings s) => JsonSerializer.SerializeToNode(provider.ConnectionStatus(s, null))!["state"]!.GetValue<string>();
Check(State(settings) == "untested", "new provider is not falsely marked connected");
await provider.Complete(input, settings, false, default);
Check(State(settings) == "success", "successful provider response updates connection state");
Check(State(settings with { Model = "different" }) == "untested", "different model does not inherit successful status");
Check(State(settings with { Enabled = false }) == "disabled", "disabled provider state");
Check(string.IsNullOrEmpty(observedAuth) && observed!["model"]!.GetValue<string>() == "fake-model", "keyless API and model override");
Check(observed!["session_id"] == null && observed["provider"] == null && observed["stream"]!.GetValue<bool>() == false, "provider-specific fields removed");
Check(observed["temperature"]!.GetValue<double>() == .9 && observed["response_format"]!["type"]!.GetValue<string>() == "json_schema", "game defaults and schema preserved");
Check(input["session_id"] != null && input["model"]!.GetValue<string>() == "old", "original request untouched");
await provider.Complete(input, settings with { ApiKey = "test-key", Temperature = .2, TopP = .4, MaxTokens = 256, OutputMode = "json" }, false, default);
Check(observedAuth == "Bearer test-key" && observed!["temperature"]!.GetValue<double>() == .2 && observed["max_tokens"]!.GetValue<int>() == 256, "key and generation overrides");
Check(observed!["response_format"]!["type"]!.GetValue<string>() == "json_object" && observed["messages"]!.AsArray().Count == 3, "schema translated into JSON instruction");
var samplingInput = (JsonObject)input.DeepClone(); samplingInput["top_p"] = .95;
int beforeSampling = requests;
await provider.Complete(samplingInput, settings with { BaseUrl = url + "/no-sampling" }, false, default);
Check(requests == beforeSampling + 3 && observed!["temperature"] == null && observed["top_p"] == null, "unsupported inherited sampling parameters removed on explicit rejection");
Check(observed!["response_format"]!["type"]!.GetValue<string>() == "json_schema", "compatibility retry preserves schema");
beforeSampling = requests;
await provider.Complete(samplingInput, settings with { BaseUrl = url + "/no-sampling" }, false, default);
Check(requests == beforeSampling + 1, "learned model capabilities avoid repeated failed requests");
await Failure(async () => await provider.Complete(samplingInput, settings with { BaseUrl = url + "/no-sampling", Temperature = .3 }, false, default), "model_parameters");
await provider.Complete(samplingInput, settings with { BaseUrl = url + "/v1" }, false, default);
Check(observed!["temperature"] != null && observed["top_p"] != null, "other providers keep inherited defaults");
foreach (var pair in new[] { ("unauthorized", "auth"), ("missing", "model_or_endpoint"), ("rate", "rate_limit"), ("redirect", "provider_http"), ("invalid", "invalid_response"), ("oversize", "size") })
    await Failure(async () => await provider.Complete(input, settings with { BaseUrl = url + "/" + pair.Item1 }, false, default), pair.Item2);
Check(State(settings with { BaseUrl = url + "/unauthorized" }) == "error", "authentication failure updates provider state");
using (var cancel = new CancellationTokenSource(150))
{
    try { await provider.Complete(input, settings with { BaseUrl = url + "/slow" }, false, cancel.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { Check(true, "caller cancellation preserved"); }
    await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3)); Check(true, "HTTP cancellation reaches provider");
}
await Failure(async () => await provider.Complete(input, settings with { BaseUrl = url + "/slow" }, false, default), "timeout");
var config = Path.Combine(output, "settings.json");
var store = new SettingsStore(config); store.Load(); Check(!store.Read().Enabled, "first launch disabled");
store.Save(settings with { ApiKey = "saved-secret" });
var reloaded = new SettingsStore(config); reloaded.Load(); Check(reloaded.Read().ApiKey == "saved-secret", "settings persist");
Check(!JsonSerializer.Serialize(reloaded.Public()).Contains("saved-secret"), "config GET does not reveal key");
Check(reloaded.Merge(new(true, url, "x")).ApiKey == "saved-secret", "blank key preserves secret");
Check(reloaded.Merge(new(true, url, "x", ClearApiKey: true)).ApiKey == "", "explicit clear removes key");
var hosted = reloaded.Merge(new(true, "https://untrusted.invalid", "ignored", "ignored-key", Temperature: 1.8, UseManifestDeX: true));
Check(hosted.UseManifestDeX && hosted.BaseUrl == settings.BaseUrl && hosted.ApiKey == "saved-secret" && hosted.Temperature == settings.Temperature, "hosted selection preserves private settings and ignores blocked edits");
SettingsStore.Validate(new Settings(Enabled: true, UseManifestDeX: true), true);
using (var hostedClient = new ManifestDeXClient(config, provider))
{
    var effective = hostedClient.Effective(hosted);
    Check(effective.BaseUrl == ManifestDeXClient.ServiceUrl + "/v1" && effective.Model == "ManifestDeX AI" && effective.ApiKey != "saved-secret", "hosted requests use only fixed service endpoint and scoped credentials");
    Check(hostedClient.Effective(settings) == settings, "private provider routing unchanged");
}
File.WriteAllText(config, "bad-json"); reloaded.Load(); Check(reloaded.Read().Enabled && reloaded.LoadError != null, "corrupt config fails closed");

// Exercise the actual companion process, security middleware, endpoints and EOF lifecycle.
string browserToken = new('b', 48), internalToken = new('i', 48);
var panelDll = Path.Combine(AppContext.BaseDirectory, "SWYF.CustomAI.Panel.dll");
using var panel = new Process { StartInfo = new("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
panel.StartInfo.ArgumentList.Add(panelDll); panel.Start(); var stderr = panel.StandardError.ReadToEndAsync();
var panelConfig = Path.Combine(output, "panel-settings.json");
await panel.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new Startup(internalToken, browserToken, panelConfig, Environment.ProcessId), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
await panel.StandardInput.FlushAsync();
var readyLine = await panel.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
if (readyLine == null) throw new Exception("Panel exited before READY: " + await stderr);
var ready = JsonNode.Parse(readyLine)!;
using var client = new HttpClient { BaseAddress = new Uri(ready["url"]!.GetValue<string>()) };
Check((await client.GetAsync("/api/config")).StatusCode == HttpStatusCode.Unauthorized, "config requires token");
client.DefaultRequestHeaders.Add("X-CustomAI-Token", browserToken);
Check((await client.GetAsync("/api/config")).IsSuccessStatusCode, "browser token accepted");
Check((await client.GetFromJsonAsync<JsonObject>("/api/manifestdex/account"))?["connected"]?.GetValue<bool>()==false, "hosted account endpoint writes JSON when disconnected");
Check((await client.PostAsJsonAsync("/api/manifestdex/poll",new{})).Content.Headers.ContentType?.MediaType=="application/json", "hosted poll endpoint writes JSON without an active flow");
client.DefaultRequestHeaders.Add("Origin", "https://example.com");
Check((await client.GetAsync("/api/config")).StatusCode == HttpStatusCode.Forbidden, "foreign origin rejected");
client.DefaultRequestHeaders.Remove("Origin");
using (var wrongHost = new HttpRequestMessage(HttpMethod.Get, "/api/config"))
{ wrongHost.Headers.Host = "example.com"; Check((await client.SendAsync(wrongHost)).StatusCode == HttpStatusCode.Forbidden, "foreign host rejected"); }
Check((await client.PostAsJsonAsync("/internal/chat/completions", input)).StatusCode == HttpStatusCode.Unauthorized, "browser token cannot call internal API");
var form = new SettingsInput(true, url + "/v1", "fake", "saved-secret");
Check((await client.PutAsJsonAsync("/api/config", form)).IsSuccessStatusCode, "save endpoint");
Check(!(await client.GetStringAsync("/api/config")).Contains("saved-secret"), "HTTP config masks API key");
Check((await client.PostAsJsonAsync("/api/test", form)).IsSuccessStatusCode, "connection test validates real schema");
Check((await client.PostAsJsonAsync("/api/test", form with { BaseUrl = url + "/wrong-schema" })).StatusCode == HttpStatusCode.BadGateway, "test rejects invalid gameplay schema");
Check((await client.GetStringAsync("/api/config")).Contains("/v1"), "test does not save draft settings");
client.DefaultRequestHeaders.Remove("X-CustomAI-Token"); client.DefaultRequestHeaders.Add("X-CustomAI-Token", internalToken);
Check((await client.GetFromJsonAsync<JsonObject>("/internal/status"))!["state"]!.GetValue<string>() == "success", "game status reflects saved provider test");
Check((await client.PostAsJsonAsync("/internal/chat/completions", input)).IsSuccessStatusCode, "internal request forwarded");
Check(!(await client.GetStringAsync("/internal/status")).Contains("saved-secret"), "game status does not expose API credentials");
Check((await client.GetAsync("/api/config")).StatusCode == HttpStatusCode.Unauthorized, "internal token cannot read browser config");
panel.StandardInput.Close(); await panel.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(6)); Check(panel.ExitCode == 0, "helper shuts down on parent pipe EOF");

// Disposable copies only: real game remains untouched by this test.
if (!args.Contains("--provider-only"))
{
var game = Path.Combine(output, "game & release test"); var managed = Path.Combine(game, "Scam With Your Friends_Data", "Managed"); Directory.CreateDirectory(managed);
var realManaged = Path.Combine(Environment.GetEnvironmentVariable("SWYF_GAME_DIR") ?? throw new InvalidOperationException("Set SWYF_GAME_DIR to your game installation folder."), "Scam With Your Friends_Data", "Managed");
foreach (var library in Directory.GetFiles(realManaged, "*.dll").Where(p => Path.GetFileName(p) is not ("Assembly-CSharp.dll" or "ScriptsAssDef.dll" or "SWYF.CustomAI.Bridge.dll")))
    File.Copy(library, Path.Combine(managed, Path.GetFileName(library)));
foreach (var name in new[] { "Assembly-CSharp.dll", "ScriptsAssDef.dll" })
{
    var live = Path.Combine(realManaged, name);
    var mod = Path.GetFullPath(Path.Combine(realManaged, "../../CustomAI"));
    var manifestPath = Path.Combine(mod, "manifest.json");
    var installed = File.Exists(manifestPath) ? JsonSerializer.Deserialize<Swyf.CustomAI.Installation.Manifest>(File.ReadAllText(manifestPath)) : null;
    var record = installed?.Files.Single(x => x.Name == name);
    var source = record != null && Installer.Hash(live) == record.PatchedHash
        ? installed!.Version < 3 ? Path.Combine(mod, "backup", name) : Path.Combine(mod, "backup", record.OriginalHash, name)
        : live;
    File.Copy(source, Path.Combine(managed, name));
}
var dist = Path.Combine(root, "dist");
var gameConfig = Path.Combine(game, "customai.toml");
var beforeInvalidInstall = Installer.Hash(Path.Combine(managed, "Assembly-CSharp.dll"));
Check(Installer.Main(["install", game, dist, "--disable-kolkata=maybe"]) != 0 && !File.Exists(gameConfig) &&
    !File.Exists(Path.Combine(game, "CustomAI", "manifest.json")) && Installer.Hash(Path.Combine(managed, "Assembly-CSharp.dll")) == beforeInvalidInstall,
    "invalid Kolkata option is rejected before configuration or game files change");
Check(Installer.Main(["install", game, dist]) == 0 && !File.Exists(gameConfig), "install without a choice preserves the default enabled backend");
Check(Installer.Main(["install", game, dist, "--disable-kolkata=true"]) == 0 && BackendConfig.Read(gameConfig), "installer Yes saves disabled Kolkata startup setting");
Check(Installer.Main(["verify", game]) == 0, "patched assembly hook verification");
using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(Path.Combine(managed, "Assembly-CSharp.dll")))
{
    var api = assembly.MainModule.Types.Single(t => t.Name == "KolkataApi");
    Check(api.Methods.Single(m => m.Name == "Awake").Body.Instructions.Any(i => i.Operand is Mono.Cecil.MethodReference m &&
        m.DeclaringType.FullName == "Swyf.CustomAI.Runtime" && m.Name == "ConfigureBackend"), "Kolkata startup config runs from Awake before OnEnable");
    Check(!assembly.MainModule.GetMemberReferences().Any(m => m.DeclaringType.FullName == "Swyf.CustomAI.Runtime" && m.Name == "SkipAuth"), "new installation has no legacy fake-auth hook");
}
const string preservedConfig = "# keep this comment and formatting\r\ndisable_kolkata_api = true # private play\r\n";
File.WriteAllText(gameConfig, preservedConfig);
Check(Installer.Main(["install", game, dist]) == 0 && File.ReadAllText(gameConfig) == preservedConfig, "reinstall without a choice preserves exact startup configuration");
Check(Installer.Main(["verify", game, dist, "--disable-kolkata=false"]) != 0 && File.ReadAllText(gameConfig) == preservedConfig, "Kolkata choice is accepted only by install");
Check(Installer.Main(["install", game, dist, "--disable-kolkata=false"]) == 0 && !BackendConfig.Read(gameConfig), "installer No re-enables Kolkata at next startup");
// Recreate the earlier four-hook layout only in disposable copies, then exercise its migration.
var legacyAssemblyPath = Path.Combine(managed, "Assembly-CSharp.dll");
using (var resolver = new Mono.Cecil.DefaultAssemblyResolver())
{
    resolver.AddSearchDirectory(managed);
    using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(legacyAssemblyPath, new Mono.Cecil.ReaderParameters { AssemblyResolver = resolver });
    var module = assembly.MainModule;
    var api = module.Types.Single(t => t.Name == "KolkataApi");
    var awake = api.Methods.Single(m => m.Name == "Awake");
    var configure = awake.Body.Instructions.Single(i => i.Operand is Mono.Cecil.MethodReference m && m.DeclaringType.FullName == "Swyf.CustomAI.Runtime" && m.Name == "ConfigureBackend");
    var runtime = ((Mono.Cecil.MethodReference)configure.Operand).DeclaringType;
    Check(configure.Previous.OpCode == Mono.Cecil.Cil.OpCodes.Ldarg_0, "startup hook passes the Kolkata component");
    awake.Body.Instructions.Remove(configure.Previous);
    configure.Operand = new Mono.Cecil.MethodReference("EnsureInitialized", module.TypeSystem.Void, runtime);
    foreach (var getter in new[] { "get_BackendAuthenticated", "get_LobbyAuthenticated" })
    {
        var method = api.Methods.Single(m => m.Name == getter);
        var first = method.Body.Instructions[0];
        var il = method.Body.GetILProcessor();
        il.InsertBefore(first, il.Create(Mono.Cecil.Cil.OpCodes.Call, new Mono.Cecil.MethodReference("SkipAuth", module.TypeSystem.Boolean, runtime)));
        il.InsertBefore(first, il.Create(Mono.Cecil.Cil.OpCodes.Brfalse, first));
        il.InsertBefore(first, il.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_1));
        il.InsertBefore(first, il.Create(Mono.Cecil.Cil.OpCodes.Ret));
    }
    assembly.Write(legacyAssemblyPath + ".legacy");
}
File.Move(legacyAssemblyPath + ".legacy", legacyAssemblyPath, true);
var legacyManifestPath = Path.Combine(game, "CustomAI", "manifest.json");
var legacyManifest = JsonNode.Parse(File.ReadAllText(legacyManifestPath))!;
legacyManifest["Version"] = 4;
legacyManifest["LegacyMetadata"] = new JsonObject { ["mode"] = "preserved" };
legacyManifest["Files"]!.AsArray().Single(f => f!["Name"]!.GetValue<string>() == "Assembly-CSharp.dll")!["PatchedHash"] = Installer.Hash(legacyAssemblyPath);
File.WriteAllText(legacyManifestPath, legacyManifest.ToJsonString());
Check(Installer.Main(["install", game, dist]) == 0 && Installer.Main(["verify", game]) == 0, "version 4 four-hook installation migrates to current hooks");
var migratedManifest = JsonNode.Parse(File.ReadAllText(legacyManifestPath))!;
Check(migratedManifest["Version"]!.GetValue<int>() == 5 && migratedManifest["LegacyMetadata"]?["mode"]?.GetValue<string>() == "preserved", "version 4 migration preserves unknown manifest metadata");
using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(legacyAssemblyPath))
    Check(!assembly.MainModule.GetMemberReferences().Any(m => m.DeclaringType.FullName == "Swyf.CustomAI.Runtime" && m.Name == "SkipAuth"), "version 4 migration removes legacy fake-auth hooks");
var savedBackendConfig = File.ReadAllText(gameConfig);
Check(Installer.Main(["uninstall", game]) == 0 && File.ReadAllText(gameConfig) == savedBackendConfig, "uninstall restores originals and preserves Kolkata configuration");
Check(Installer.Main(["verify", game, dist]) == 0, "restored files remain compatible");
var releaseZip = Path.Combine(root, "artifacts", "SWYF-Custom-AI-win-x64.zip");
using (var archive = System.IO.Compression.ZipFile.OpenRead(releaseZip))
{
    Check(archive.Entries.Any(e => e.FullName == "Install Custom AI.cmd") && archive.Entries.All(e =>
        !e.FullName.EndsWith("settings.json") && !e.FullName.Contains("/backup/") && !e.FullName.EndsWith("Assembly-CSharp.dll") && !e.FullName.EndsWith("ScriptsAssDef.dll")), "release ZIP has root installer and no game files or secrets");
}
System.IO.Compression.ZipFile.ExtractToDirectory(releaseZip, game, true);
File.WriteAllText(Path.Combine(game, "Scam With Your Friends.exe"), "test presence marker; never executed");
async Task<int> RunWrapper(string name, string input = "")
{
    using var wrapper = new Process { StartInfo = new ProcessStartInfo("cmd.exe", "/d /s /c \"\"" + Path.Combine(game, name) + "\"\"")
        { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = output, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
    wrapper.Start();
    var stdout = wrapper.StandardOutput.ReadToEndAsync(); var errors = wrapper.StandardError.ReadToEndAsync();
    await wrapper.StandardInput.WriteLineAsync(input); wrapper.StandardInput.Close();
    await wrapper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
    if (wrapper.ExitCode != 0) Console.WriteLine(await stdout + await errors);
    return wrapper.ExitCode;
}
Check(await RunWrapper("Install Custom AI.cmd", "Y") == 0 && BackendConfig.Read(gameConfig), "double-click installer Yes handles spaces and ampersands from another working directory");
Check(await RunWrapper("Install Custom AI.cmd", "N") == 0 && !BackendConfig.Read(gameConfig), "double-click installer No saves Kolkata enabled");
Check(await RunWrapper("Uninstall Custom AI.cmd") == 0, "double-click uninstall restores extracted installation");
File.WriteAllText(Path.Combine(game, "steam_appid.txt"), "not-a-steam-id");
Check(await RunWrapper("Play with Custom AI.cmd") != 0, "play wrapper stops on invalid Steam metadata without launching");
void SimulateUpdate(string name, bool breakHook = false)
{
    var path = Path.Combine(managed, name);
    using var resolver = new Mono.Cecil.DefaultAssemblyResolver(); resolver.AddSearchDirectory(managed);
    using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(path, new Mono.Cecil.ReaderParameters { AssemblyResolver = resolver });
    assembly.MainModule.Mvid = Guid.NewGuid();
    if (breakHook) assembly.MainModule.Types.Single(t => t.Name == "KolkataApi").Methods.Single(m => m.Name == "CompleteOpenRouterAsync").Name = "ChangedCompletionContract";
    assembly.Write(path + ".updated");
    assembly.Dispose();
    File.Move(path + ".updated", path, true);
}
var originalAi = Installer.Hash(Path.Combine(managed, "Assembly-CSharp.dll"));
SimulateUpdate("Assembly-CSharp.dll");
var updatedAi = Installer.Hash(Path.Combine(managed, "Assembly-CSharp.dll"));
Check(updatedAi != originalAi && Installer.Main(["verify", game, dist]) == 0, "new compatible game hash accepted");
Check(Installer.Main(["install", game, dist]) == 0, "compatible update installs without rebuilding bridge");
var manifestFile = Path.Combine(game, "CustomAI", "manifest.json");
var manifest = JsonSerializer.Deserialize<Swyf.CustomAI.Installation.Manifest>(File.ReadAllText(manifestFile))!;
var uiRecord = manifest.Files.Single(f => f.Name == "ScriptsAssDef.dll");
File.Copy(Path.Combine(game, "CustomAI", "backup", uiRecord.OriginalHash, uiRecord.Name), Path.Combine(managed, uiRecord.Name), true);
SimulateUpdate("ScriptsAssDef.dll");
var updatedUi = Installer.Hash(Path.Combine(managed, "ScriptsAssDef.dll"));
Check(Installer.Main(["install", game, dist]) == 0, "partial Steam update repaired using current generation");
Check(Installer.Main(["uninstall", game]) == 0 && Installer.Hash(Path.Combine(managed, "Assembly-CSharp.dll")) == updatedAi && Installer.Hash(Path.Combine(managed, "ScriptsAssDef.dll")) == updatedUi, "uninstall restores latest generation rather than original release");
Check(Installer.Main(["install", game, dist]) == 0, "reinstall updated generation");
manifest = JsonSerializer.Deserialize<Swyf.CustomAI.Installation.Manifest>(File.ReadAllText(manifestFile))!;
var aiRecord = manifest.Files.Single(f => f.Name == "Assembly-CSharp.dll");
File.Copy(Path.Combine(game, "CustomAI", "backup", aiRecord.OriginalHash, aiRecord.Name), Path.Combine(managed, aiRecord.Name), true);
SimulateUpdate("Assembly-CSharp.dll", breakHook: true);
var incompatibleHash = Installer.Hash(Path.Combine(managed, "Assembly-CSharp.dll"));
var unchangedUi = Installer.Hash(Path.Combine(managed, "ScriptsAssDef.dll"));
Check(Installer.Main(["install", game, dist]) != 0 && Installer.Hash(Path.Combine(managed, "Assembly-CSharp.dll")) == incompatibleHash && Installer.Hash(Path.Combine(managed, "ScriptsAssDef.dll")) == unchangedUi, "incompatible update rejected before either live assembly changes");
Check(Installer.Main(["uninstall", game]) == 0 && Installer.Hash(Path.Combine(managed, "Assembly-CSharp.dll")) == incompatibleHash, "uninstall preserves incompatible Steam replacement");
}
if (args.Contains("--live"))
{
    var livePath = Path.Combine(Environment.GetEnvironmentVariable("SWYF_GAME_DIR") ?? throw new InvalidOperationException("Set SWYF_GAME_DIR to your game installation folder."), "CustomAI", "settings.json");
    var live = JsonSerializer.Deserialize<Settings>(File.ReadAllText(livePath), SettingsStore.Json)!;
    var liveInput = (JsonObject)samplingInput.DeepClone();
    liveInput["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "Reply with JSON: dialogue is a greeting, trust_percent is 50, emotion is NEUTRAL." });
    liveInput["response_format"] = JsonNode.Parse("""{"type":"json_schema","json_schema":{"name":"caller_turn","strict":true,"schema":{"type":"object","properties":{"dialogue":{"type":"string"},"trust_percent":{"type":"integer","minimum":0,"maximum":100},"emotion":{"type":"string","enum":["TRUSTING","SUSPICIOUS","ANGRY","NEUTRAL"]}},"required":["dialogue","trust_percent","emotion"],"additionalProperties":false}}}""");
    var liveResponse = await provider.Complete(liveInput, live, false, default);
    var text = JsonNode.Parse(Provider.ExtractText(liveResponse))!;
    Check(text["dialogue"]!.GetValue<string>().Length > 0 && text["trust_percent"]!.GetValue<int>() == 50 && text["emotion"]!.GetValue<string>() == "NEUTRAL", "configured live provider returns valid caller JSON with game defaults");
}
await fake.StopAsync(); await fake.DisposeAsync();

using(var handler=new PairingHandler())
using(var pairing=new ManifestDeXClient(Path.Combine(output,"race","settings.json"),provider,handler)){
 await pairing.Connect(default);var pending=pairing.Poll(default);await handler.Started.Task;
 pairing.Disconnect();handler.Release.TrySetResult();await pending;
 Check(pairing.Effective(new Settings(UseManifestDeX:true)).ApiKey=="","Disconnect ignores a late OAuth completion");
}
using(var handler=new PairingHandler())
using(var pairing=new ManifestDeXClient(Path.Combine(output,"single-flight","settings.json"),provider,handler)){
 await pairing.Connect(default);handler.Release.TrySetResult();await pairing.Poll(default);
 await Task.WhenAll(Enumerable.Range(0,8).Select(_=>pairing.Account(default)));
 Check(handler.AccountCalls==1,"concurrent account refreshes share one combined service request");
}

using(var handler=new RateLimitedPairingHandler())
using(var pairing=new ManifestDeXClient(Path.Combine(output,"rate-limit","settings.json"),provider,handler)){
 for(int i=0;i<2;i++){
  try{await pairing.Connect(default);throw new Exception("Expected rate limit");}
  catch(ApiFailure e){Check(e.Status==429&&e.RetryAfter is >590 and <=600,"pairing preserves Retry-After and caches cooldown");}
 }
 Check(handler.Calls==1,"repeated pairing clicks do not resend during cooldown");
}

Console.WriteLine($"ALL {passed} CHECKS PASSED");
File.WriteAllText(Path.Combine(output, "result.txt"), $"{passed} checks passed at {DateTimeOffset.UtcNow:O}");
sealed class PairingHandler:HttpMessageHandler
{
 public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public int AccountCalls;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
 {
  if(request.RequestUri!.AbsolutePath=="/api/auth/start")return Json("""{"flowId":"test-flow","completionKey":"test-key","authorizationUrl":"https://swyf-ai.manifestdex.com/oauth/start"}""");
  if(request.RequestUri.AbsolutePath=="/api/auth/poll"){Started.TrySetResult();await Release.Task;return Json("""{"status":"approved","ticket":"swyf2_race_test_ticket"}""");}
  if(request.RequestUri.AbsolutePath=="/api/me"){Interlocked.Increment(ref AccountCalls);await Task.Delay(100,ct);return Json("""{"user":{"id":"m"},"quota":{"windows":[]},"service":{"available":true}}""");}
  return Json("{}");
 }
 private static HttpResponseMessage Json(string value)=>new(HttpStatusCode.OK){Content=new StringContent(value,Encoding.UTF8,"application/json")};
}
sealed class RateLimitedPairingHandler:HttpMessageHandler
{
 public int Calls;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){
  Interlocked.Increment(ref Calls);await Task.Delay(10,ct);
  var response=new HttpResponseMessage(HttpStatusCode.TooManyRequests){Content=new StringContent("""{"error":"rate_limit","message":"Too many requests."}""",Encoding.UTF8,"application/json")};
  response.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(10));return response;
 }
}
