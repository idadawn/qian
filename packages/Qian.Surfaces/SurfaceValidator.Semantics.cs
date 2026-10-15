using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces;

public sealed partial class SurfaceValidator
{
    private sealed partial class Validation
    {
        private static bool Match(string value, string pattern) => Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        private void ManifestRules()
        {
            if (Strings(_manifest, "inject").Intersect(Strings(_manifest, "provides"), StringComparer.Ordinal).Any())
                Error(ManifestFile, "/provides", SurfaceErrors.Policy, "inject 与 provides 不能相交。");
            // 宿主给出目标装配的服务键目录时，inject 必须全部有 Provider（PRD 7.2.1）；optionalInject 允许缺席。
            if (options.ServiceKeys is { } provided)
            {
                var inject = Strings(_manifest, "inject");
                for (var index = 0; index < inject.Length; index++)
                    if (!provided.Contains(inject[index], StringComparer.Ordinal))
                        Error(ManifestFile, "/inject/" + index, SurfaceErrors.Reference, "inject 的服务键在目标装配中没有 Provider。");
            }
            var policy = _manifest.GetProperty("policy");
            if (_id.StartsWith("qian.", StringComparison.Ordinal) && _tier is not ("system" or "official"))
                Error(ManifestFile, "/id", SurfaceErrors.Policy, "qian. 前缀保留给平台与官方组件。");
            if (policy.TryGetProperty("required", out var required) && required.GetBoolean() && _tier != "system")
                Error(ManifestFile, "/policy/required", SurfaceErrors.Policy, "required 只允许平台插件声明。");
            if (_manifest.TryGetProperty("surfaces", out var pointers))
                foreach (var pointer in pointers.EnumerateObject()) Safe(pointer.Value.GetString()!, ManifestFile, "/surfaces/" + pointer.Name, required: true);
            var runtime = _manifest.TryGetProperty("runtime", out var declared) ? declared : default;
            var form = runtime.ValueKind == JsonValueKind.Object ? Text(runtime, "form") : "";
            if (Directory.Exists(Path.Combine(root, "src")) && form.Length == 0)
                Error(ManifestFile, "/runtime/form", SurfaceErrors.Schema, "存在 src 时必须声明运行时形态。");
            if (_tier == "community" && runtime.ValueKind == JsonValueKind.Object && runtime.EnumerateObject().Any())
                Error(ManifestFile, "/runtime", SurfaceErrors.Policy, "S1 的 community 组件必须无代码。");
            if (form == "module" && _tier is not ("system" or "official"))
                Error(ManifestFile, "/runtime/form", SurfaceErrors.Policy, "共享进程代码只允许 system/official。");
            if (form == "service" && (_tier is not ("system" or "official") || Text(policy, "sandbox") != "workload"))
                Error(ManifestFile, "/policy/sandbox", SurfaceErrors.Policy, "S1 service 必须为受信任工作负载。");
            if (form is "function" or "package") Error(ManifestFile, "/runtime/form", SurfaceErrors.Policy, "该运行时形态保留给 S2。", "warning");
            if (runtime.ValueKind == JsonValueKind.Object)
            {
                if (runtime.TryGetProperty("nuget", out var packages) && packages.GetArrayLength() > 0 &&
                    (!_manifest.TryGetProperty("engines", out var engines) || Text(engines, "kun").Length == 0))
                    Error(ManifestFile, "/engines/kun", SurfaceErrors.Schema, "NuGet 组件必须声明 Kun 版本范围。");
                if (runtime.TryGetProperty("configSchema", out var schema))
                {
                    var path = Safe(schema.GetString()!, ManifestFile, "/runtime/configSchema", required: true);
                    if (path is not null)
                    {
                        var parsed = SurfaceReader.Parse(Relative(path), File.ReadAllText(path), false, trace);
                        _diagnostics.AddRange(parsed.Diagnostics);
                        if (parsed.Syntax is { } syntax && !validator._schemas.IsSchema(syntax.Data))
                            Error(Relative(path), "", SurfaceErrors.Schema, "配置 Schema 无效。");
                    }
                }
            }
        }

        private void SemanticRules()
        {
            var tools = new HashSet<string>(options.Tools, StringComparer.Ordinal);
            var skills = new HashSet<string>(options.Skills, StringComparer.Ordinal);
            foreach (var doc in _documents.Where(d => d.Kind == "tool")) tools.Add(_id + "/" + doc.Name);
            foreach (var doc in _documents.Where(d => d.Kind == "skill")) skills.Add(_id + "/" + doc.Name);
            foreach (var group in _documents.GroupBy(d => (d.Kind, d.Name)).Where(g => g.Count() > 1))
                foreach (var doc in group.Skip(1)) Error(doc.File, "/name", SurfaceErrors.Name, "表面名称重复。");
            foreach (var doc in _documents)
            {
                if (doc.Kind is "skill" or "agent" or "command") MarkdownRules(doc, tools, skills);
                if (doc.Kind == "tool") ToolRules(doc);
                if (doc.Kind == "hooks") HookRules(doc);
                if (doc.Kind == "mcp") McpRules(doc);
                if (doc.Kind == "patch")
                {
                    var identities = new HashSet<string>(StringComparer.Ordinal); var index = 0;
                    foreach (var entry in doc.Data.EnumerateArray())
                    {
                        var scope = Text(entry, "scope");
                        var identity = (scope.Length == 0 ? "global" : scope) + "/" + Text(entry, "id");
                        if (!identities.Add(identity)) Error(doc.File, "/" + index + "/id", SurfaceErrors.Name, "同一 patch 中条目身份重复。");
                        index++;
                    }
                }
                ScanSecrets(doc.Data, doc.File, "", "");
            }
        }

        private void MarkdownRules(SurfaceDocument doc, HashSet<string> tools, HashSet<string> skills)
        {
            if (doc.Kind != "command" && Text(doc.Data, "name") != doc.Name)
                Error(doc.File, "/name", SurfaceErrors.Name, "name 必须与目录或文件名一致。");
            if (!Match(doc.Name, "^[a-z][a-z0-9-]*$")) Error(doc.File, "/name", SurfaceErrors.Name, "表面名称必须为小写短横线形式。");
            if (string.IsNullOrWhiteSpace(doc.Body)) Error(doc.File, "", SurfaceErrors.Schema, "Markdown 正文不能为空。");
            var field = doc.Kind == "agent" ? "tools" : "allowed-tools";
            var allowed = Strings(doc.Data, field);
            for (var i = 0; i < allowed.Length; i++)
            {
                if (allowed[i].StartsWith("shell(", StringComparison.Ordinal) && doc.Kind == "command") continue;
                Reference(doc.File, "/" + field + "/" + i, allowed[i], tools);
            }
            var references = Strings(doc.Data, "skills");
            for (var i = 0; i < references.Length; i++) Reference(doc.File, "/skills/" + i, references[i], skills);
            foreach (Match inline in Regex.Matches(doc.Body, "!`([^`]+)`", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            {
                var command = inline.Groups[1].Value.Trim();
                var permitted = allowed.Any(value => value.StartsWith("shell(", StringComparison.Ordinal) && value.EndsWith(')') &&
                    (command == value[6..^1] || command.StartsWith(value[6..^1] + " ", StringComparison.Ordinal)));
                if (doc.Kind != "command" || options.ExecutionPlane != "human-cli" || _tier is not ("system" or "official") ||
                    options.Profile is "gateway" or "identity" || (options.Profile == "platform" && !options.AllowCommandHooks) ||
                    !permitted || Match(command, "[|&;<>\\r\\n]") || command.Contains("$(", StringComparison.Ordinal))
                    Error(doc.File, "/allowed-tools", SurfaceErrors.Policy, "内联 shell 仅限受信任组件的人类 CLI，并需覆盖固定 argv 前缀。");
            }
            foreach (Match link in Regex.Matches(doc.Body, "\\[[^\\]]*\\]\\(([^)]+)\\)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            {
                var target = link.Groups[1].Value.Split('#')[0];
                if (target.Length == 0 || Uri.TryCreate(target, UriKind.Absolute, out _)) continue;
                Safe(Path.Combine(Path.GetDirectoryName(doc.File)!, target), doc.File, "", required: true);
            }
            if (doc.Kind == "command")
                foreach (Match include in Regex.Matches(doc.Body, "(?:^|\\s)@([A-Za-z0-9_./-]+)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                    Safe(include.Groups[1].Value, doc.File, "", required: true);
        }

        private void Reference(string file, string pointer, string reference, HashSet<string> available)
        {
            var exists = reference.EndsWith("/*", StringComparison.Ordinal)
                ? available.Any(name => name.StartsWith(reference[..^1], StringComparison.Ordinal)) : available.Contains(reference);
            if (exists) return;
            var external = reference.Contains('/') && !reference.StartsWith(_id + "/", StringComparison.Ordinal);
            Error(file, pointer, SurfaceErrors.Reference, "工具或技能引用不存在于当前目录。", external && !options.CatalogComplete ? "warning" : "error");
        }

        private void ToolRules(SurfaceDocument doc)
        {
            if (Path.GetFileNameWithoutExtension(doc.File) != doc.Name) Error(doc.File, "/name", SurfaceErrors.Name, "工具名称必须与文件名一致。");
            var input = doc.Data.GetProperty("inputSchema");
            TenantFields(input, "/inputSchema");
            if (!validator._schemas.IsSchema(input)) Error(doc.File, "/inputSchema", SurfaceErrors.Schema, "输入 JSON Schema 无效。");
            try
            {
                if (ToolContractHash.Compute(doc.Data) != Text(doc.Data, "schemaSha256")) Error(doc.File, "/schemaSha256", SurfaceErrors.Hash, "工具描述哈希与 JCS 内容不一致。");
            }
            catch (JsonException) { Error(doc.File, "/inputSchema", SurfaceErrors.Schema, "工具内容不符合 JCS 数值或 Unicode 约束。"); }
            var binding = doc.Data.GetProperty("binding");
            switch (Text(binding, "kind"))
            {
                case "http": Egress(doc.File, "/binding/url", Text(binding, "url")); break;
                case "command":
                    if (!options.Capabilities.Contains(Text(binding, "id"), StringComparer.Ordinal)) Error(doc.File, "/binding/id", SurfaceErrors.Reference, "命令能力未登记。");
                    break;
                case "service":
                    var key = Text(binding, "key");
                    if (!Strings(_manifest, "inject").Concat(Strings(_manifest, "provides")).Contains(key, StringComparer.Ordinal) ||
                        !options.Capabilities.Contains(key + "." + Text(binding, "method"), StringComparer.Ordinal))
                        Error(doc.File, "/binding", SurfaceErrors.Reference, "服务键或方法未在目录中登记。");
                    break;
            }
            void TenantFields(JsonElement element, string pointer)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in element.EnumerateObject())
                    {
                        var child = pointer + "/" + Diagnostics.Token(property.Name);
                        if (property.Name == "properties" && property.Value.ValueKind == JsonValueKind.Object)
                            foreach (var field in property.Value.EnumerateObject())
                                if (Match(field.Name.ToLowerInvariant(), "^tenant([_-]?id)?$")) Error(doc.File, child + "/" + Diagnostics.Token(field.Name), SurfaceErrors.Policy, "输入 Schema 不允许租户字段。");
                        TenantFields(property.Value, child);
                    }
                }
                else if (element.ValueKind == JsonValueKind.Array)
                { var index = 0; foreach (var child in element.EnumerateArray()) TenantFields(child, pointer + "/" + index++); }
            }
        }

        private void HookRules(SurfaceDocument doc)
        {
            foreach (var eventEntry in doc.Data.GetProperty("hooks").EnumerateObject())
            {
                var pointer = "/hooks/" + Diagnostics.Token(eventEntry.Name);
                if (!Rules.GetProperty("qianEvents").EnumerateArray().Any(e => e.GetString() == eventEntry.Name) && !options.Events.Contains(eventEntry.Name, StringComparer.Ordinal))
                {
                    if (Match(eventEntry.Name, "^[A-Z][A-Za-z0-9]+$"))
                    {
                        if (!Rules.GetProperty("agentEvents").EnumerateArray().Any(e => e.GetString() == eventEntry.Name)) Error(doc.File, pointer, SurfaceErrors.Reference, "未知 Agent 宿主事件，保留透传。", "warning");
                    }
                    else Error(doc.File, pointer, SurfaceErrors.Reference, "Qian 事件未登记。");
                }
                var groupIndex = 0;
                foreach (var group in eventEntry.Value.EnumerateArray())
                {
                    var groupPointer = pointer + "/" + groupIndex++;
                    if (group.TryGetProperty("matcher", out var matcher))
                    {
                        try { _ = new Regex(matcher.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); }
                        catch (ArgumentException) { Error(doc.File, groupPointer + "/matcher", SurfaceErrors.Syntax, "matcher 不是合法正则。"); }
                    }
                    var actionIndex = 0;
                    foreach (var action in group.GetProperty("hooks").EnumerateArray())
                    {
                        var actionPointer = groupPointer + "/hooks/" + actionIndex++;
                        switch (Text(action, "type"))
                        {
                            case "plugin":
                                if (_tier is "private" or "community") Error(doc.File, actionPointer + "/type", SurfaceErrors.Policy, "该信任等级只允许 HTTP hook。");
                                if (!options.Capabilities.Contains(Text(action, "plugin") + "/" + Text(action, "handler"), StringComparer.Ordinal))
                                    Error(doc.File, actionPointer + "/handler", SurfaceErrors.Reference, "hook 能力或 handler 未登记。");
                                break;
                            case "http": Egress(doc.File, actionPointer + "/url", Text(action, "url")); break;
                            case "command":
                                var argv = Strings(action, "command");
                                var executable = Path.GetFileName(argv[0].Replace('\\', '/')).ToLowerInvariant();
                                if (_tier is not ("system" or "official") || !options.AllowCommandHooks || options.Profile is "gateway" or "identity" ||
                                    Rules.GetProperty("shells").EnumerateArray().Any(e => e.GetString() == executable) || argv.Any(a => Match(a, "[|&;<>`*?\\r\\n]") || a.Contains("$(", StringComparison.Ordinal)))
                                    Error(doc.File, actionPointer + "/command", SurfaceErrors.Policy, "hook 只允许显式开启的受限 argv，不能执行 shell。");
                                break;
                        }
                    }
                }
            }
        }

        private void McpRules(SurfaceDocument doc)
        {
            foreach (var server in doc.Data.GetProperty("mcpServers").EnumerateObject())
            {
                var pointer = "/mcpServers/" + Diagnostics.Token(server.Name);
                var profiles = Strings(server.Value, "profiles");
                var active = profiles.Length == 0 || profiles.Contains(options.Profile, StringComparer.Ordinal);
                if (Text(server.Value, "type") == "stdio" && active && options.Profile is not ("standalone" or "minimal"))
                    Error(doc.File, pointer + "/type", SurfaceErrors.Policy, "当前 profile 不允许 stdio MCP。");
                if (Text(server.Value, "type") == "http") Egress(doc.File, pointer + "/url", Text(server.Value, "url"));
            }
        }

        private void Egress(string file, string pointer, string value)
        {
            if (!Uri.TryCreate(value.Replace("{tenant}", "tenant", StringComparison.Ordinal), UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("https" or "http") || uri.UserInfo.Length > 0)
            { Error(file, pointer, SurfaceErrors.Policy, "出网地址必须为不含凭据的 HTTP(S) URL。"); return; }
            var allowed = Strings(_manifest.GetProperty("policy"), "egress").Any(rule =>
            {
                var parts = rule.Split(':'); var host = parts[0];
                var matches = host.StartsWith("*.", StringComparison.Ordinal) ? uri.Host.EndsWith(host[1..], StringComparison.OrdinalIgnoreCase) : uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase);
                return matches && (parts.Length == 1 || int.TryParse(parts[1], out var port) && port == uri.Port);
            });
            if (!allowed) Error(file, pointer, SurfaceErrors.Policy, "出网域名或端口未在 policy.egress 中声明。");
        }

        private void ScanSecrets(JsonElement element, string file, string pointer, string name)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject()) ScanSecrets(property.Value, file, pointer + "/" + Diagnostics.Token(property.Name), property.Name);
            else if (element.ValueKind == JsonValueKind.Array)
            { var index = 0; foreach (var child in element.EnumerateArray()) ScanSecrets(child, file, pointer + "/" + index++, ""); }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var value = element.GetString()!;
                var normalized = name.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
                var sensitive = normalized is "authorization" or "cookie" or "setcookie" or "secret" or "password" or "token" or "apikey" or "accesskey" or "secretkey" || normalized.EndsWith("token", StringComparison.Ordinal) || normalized.EndsWith("password", StringComparison.Ordinal) || normalized.EndsWith("secret", StringComparison.Ordinal);
                if (sensitive && !Match(value, "^(Bearer )?secretRef://(platform|tenant|dev)/[^\\s]+$"))
                    Error(file, pointer, SurfaceErrors.Secret, "敏感字段只允许 secretRef，不允许明文。");
            }
        }
    }
}
