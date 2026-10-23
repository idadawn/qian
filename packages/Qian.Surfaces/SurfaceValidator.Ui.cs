using System.Text.Json;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces;

public sealed partial class SurfaceValidator
{
    private sealed partial class Validation
    {
        private void UiRules(SurfaceDocument doc)
        {
            var exposed = Strings(doc.Data.GetProperty("federation"), "exposes").ToHashSet(StringComparer.Ordinal);
            var slots = new HashSet<string>(StringComparer.Ordinal);
            var routes = new HashSet<string>(StringComparer.Ordinal);
            var hasBridge = doc.Data.TryGetProperty("bridge", out var bridge);
            var allowedSlots = hasBridge ? Strings(bridge, "slots").ToHashSet(StringComparer.Ordinal) : [];
            void Expose(JsonElement item, string pointer)
            {
                if (!exposed.Contains(Text(item, "expose"))) Error(doc.File, pointer + "/expose", SurfaceErrors.Reference, "UI 引用的 expose 未在 federation.exposes 声明。");
            }
            void Route(JsonElement item, string pointer)
            {
                if (!routes.Add(Text(item, "route"))) Error(doc.File, pointer + "/route", SurfaceErrors.Name, "UI 路由重复。");
            }
            foreach (var (page, pointer) in Items("pages"))
            {
                Expose(page, pointer); Route(page, pointer);
                slots.Add("qian.shell.nav/v1:" + Text(page, "route"));
                if (hasBridge && !allowedSlots.Contains("qian.shell.nav/v1")) Error(doc.File, pointer, SurfaceErrors.Policy, "桥接未允许页面导航插槽。");
            }
            var widgetTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (widget, pointer) in Items("widgets"))
            {
                Expose(widget, pointer);
                if (!widgetTypes.Add(Text(widget, "type"))) Error(doc.File, pointer + "/type", SurfaceErrors.Name, "UI 控件类型重复。");
            }
            var cards = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (card, pointer) in Items("settingsCards"))
            {
                Expose(card, pointer);
                if (!cards.Add(Text(card, "scope") + ":" + Text(card, "expose"))) Error(doc.File, pointer + "/expose", SurfaceErrors.Name, "同一 scope 的设置卡片重复。");
            }
            foreach (var (contribution, pointer) in Items("contributions"))
            {
                Expose(contribution, pointer); var slot = Text(contribution, "slot");
                if (!slots.Add(slot + ":" + Text(contribution, "id"))) Error(doc.File, pointer + "/id", SurfaceErrors.Name, "同一插槽的 UI 贡献标识重复。");
                if (!allowedSlots.Contains(slot)) Error(doc.File, pointer + "/slot", SurfaceErrors.Policy, "桥接未允许此插槽。");
                if (slot == "qian.shell.nav/v1") Route(contribution, pointer);
            }
            if (hasBridge)
            {
                var capabilities = new HashSet<string>(StringComparer.Ordinal); var index = 0;
                foreach (var capability in bridge.GetProperty("capabilities").EnumerateArray())
                {
                    if (!capabilities.Add(Text(capability, "id"))) Error(doc.File, "/bridge/capabilities/" + index + "/id", SurfaceErrors.Name, "桥接能力标识重复。");
                    index++;
                }
            }
            IEnumerable<(JsonElement Item, string Pointer)> Items(string name)
            {
                if (!doc.Data.TryGetProperty(name, out var items)) yield break;
                var index = 0;
                foreach (var item in items.EnumerateArray()) yield return (item, "/" + name + "/" + index++);
            }
        }
    }
}
