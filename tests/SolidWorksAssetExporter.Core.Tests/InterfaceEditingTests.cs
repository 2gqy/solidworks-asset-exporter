using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorksAssetExporter.AddIn;
using SolidWorksAssetExporter.Core;

namespace SolidWorksAssetExporter.Core.Tests
{
    internal static partial class Program
    {
        private static InterfacePropertySnapshot InterfaceSnapshot()
        {
            var result = new InterfacePropertySnapshot();
            foreach (var p in new Dictionary<string, string> { { "class", "movable" }, { "is_fixture", "1" }, { "connection_interface", "" }, { "accepts_interfaces", "old" }, { "asset_version", "1" } })
            { result.Raw[p.Key] = p.Value; result.Values[p.Key] = p.Value; result.Types[p.Key] = 30; }
            return result;
        }
        private static void RunInterfaceEditingTests()
        {
            Run("Interface suggestions preserve existing capability and deduplicate 200 children", delegate {
                var result = InterfaceEditRules.Suggest("old;other", Enumerable.Repeat("tip-A", 200));
                Equal("old;other;tip-A", string.Join(";", result)); Equal("old;other", string.Join(";", InterfaceEditRules.Suggest("old;other", new string[0])));
                Throws<ValidationException>(() => InterfaceEditRules.Suggest("tip-A", new[] { "TIP-A" }));
                Throws<ValidationException>(() => InterfaceEditRules.Suggest("old", new[] { "two;values" }));
            });
            Run("Interface editing requires version bump except explicit unpublished sources", delegate {
                var s = InterfaceSnapshot(); Throws<ValidationException>(() => InterfaceEditRules.Changes(s, "", new[] { "old", "tip" }, 1, false));
                var changed = InterfaceEditRules.Changes(s, "", new[] { "old", "tip" }, 2, false); Equal("2", changed["asset_version"]);
                True(!InterfaceEditRules.Changes(s, "", new[] { "old", "tip" }, 1, true).ContainsKey("asset_version"));
                Throws<ValidationException>(() => InterfaceEditRules.Changes(s, "", new[] { "tip" }, 0, true));
                Equal(0, InterfaceEditRules.Changes(s, "", new[] { "old" }, 1, false).Count);
            });
            Run("Version-only edits save without rewriting interface properties", delegate {
                var target = new InterfaceTargetFake();
                target.State.Raw[PropertyRules.AcceptsInterfaces] = " old ";
                target.State.Values[PropertyRules.AcceptsInterfaces] = " old ";
                var before = target.Read();
                var increase = InterfaceEditRules.Changes(before, "", new[] { "old" }, 3, false);
                Equal(1, increase.Count);
                InterfacePropertyWriter.Apply(target, before, increase);
                Equal(3, target.State.Version);
                Equal(" old ", target.State.Raw[PropertyRules.AcceptsInterfaces]);
                var raised = target.Read();
                Throws<ValidationException>(() => InterfaceEditRules.Changes(raised, "", new[] { "old" }, 2, false));
                var correction = InterfaceEditRules.Changes(raised, "", new[] { "old" }, 2, true);
                InterfacePropertyWriter.Apply(target, raised, correction);
                Equal(2, target.State.Version);
                Equal(2, target.Saves);
            });
            Run("Interface editing checks the exact asset version in the published registry", delegate {
                var uuid = "aaaaaaaa-aaaa-5aaa-8aaa-aaaaaaaaaaaa";
                var registered = new[] { new AssetRegistration { Uuid = uuid, Version = 1 },
                    new AssetRegistration { Uuid = uuid, Version = 3 },
                    new AssetRegistration { Uuid = "bbbbbbbb-bbbb-5bbb-8bbb-bbbbbbbbbbbb", Version = 2 } };
                Equal((int?)4, InterfaceEditRules.NextVersionIfPublished(registered, uuid, 1));
                Equal((int?)null, InterfaceEditRules.NextVersionIfPublished(registered, uuid, 2));
                Equal((int?)null, InterfaceEditRules.NextVersionIfPublished(registered, uuid, 4));
            });
            Run("Interface editor preserves role validation and blocks linked fields", delegate {
                var s = InterfaceSnapshot();
                Equal("", InterfaceEditRules.Changes(s, "", new string[0], 2, false)[PropertyRules.AcceptsInterfaces]);
                var tool = InterfaceSnapshot(); tool.Values[PropertyRules.IsTool] = "1";
                Throws<ValidationException>(() => InterfaceEditRules.Changes(tool, "", new[] { "new" }, 2, false));
                s.Linked.Add("accepts_interfaces"); Throws<ValidationException>(() => InterfaceEditRules.Changes(s, "", new[] { "tip" }, 2, false));
                Throws<ValidationException>(() => InterfaceEditRules.Changes(InterfaceSnapshot(), "x;y", new[] { "old" }, 2, false));
            });
            Run("Interface transaction rejects read-only dirty and stale targets before writing", delegate {
                var expected = InterfaceSnapshot(); var changes = InterfaceEditRules.Changes(expected, "", new[] { "new" }, 2, false);
                var t = new InterfaceTargetFake { ReadOnly = true }; Throws<ValidationException>(() => InterfacePropertyWriter.Apply(t, expected, changes)); Equal(0, t.Writes);
                t.ReadOnly = false; t.Dirty = true; Throws<ValidationException>(() => InterfacePropertyWriter.Apply(t, expected, changes)); Equal(0, t.Writes);
                t.Dirty = false; t.State.Raw["class"] = "station"; Throws<ValidationException>(() => InterfacePropertyWriter.Apply(t, expected, changes)); Equal(0, t.Writes);
            });
            Run("Interface transaction saves once and rolls back partial failure", delegate {
                var s = InterfaceSnapshot(); var changes = InterfaceEditRules.Changes(s, "", new[] { "new" }, 2, false);
                var good = new InterfaceTargetFake(); InterfacePropertyWriter.Apply(good, s, changes); Equal(1, good.Saves); Equal("new", good.State.Get("accepts_interfaces"));
                var fail = new InterfaceTargetFake { FailSave = true }; Throws<ValidationException>(() => InterfacePropertyWriter.Apply(fail, s, changes));
                Equal("old", fail.State.Get("accepts_interfaces")); Equal("1", fail.State.Get("asset_version")); True(fail.Dirty);
                var partial = new InterfaceTargetFake { FailWrite = 2 }; Throws<ValidationException>(() => InterfacePropertyWriter.Apply(partial, s, changes));
                Equal("old", partial.State.Get("accepts_interfaces")); Equal("1", partial.State.Get("asset_version")); Equal(0, partial.Saves);
            });
            Run("Interface transaction restores originally absent property on failure", delegate {
                var s = InterfaceSnapshot(); s.Raw.Remove("connection_interface"); s.Values.Remove("connection_interface"); s.Types.Remove("connection_interface");
                var t = new InterfaceTargetFake { State = s, FailSave = true };
                Throws<ValidationException>(() => InterfacePropertyWriter.Apply(t, s, new Dictionary<string, string> { { "connection_interface", "new" } }));
                True(!t.State.Raw.ContainsKey("connection_interface"));
            });
            Run("Managed slot capacity is saved with the asset version and preserved when omitted", delegate {
                var target = new InterfaceTargetFake();
                var before = target.Read();
                Throws<ValidationException>(() => InterfaceEditRules.Changes(before, "", new[] { "old" }, -1, 2, false));
                var changes = InterfaceEditRules.Changes(before, "", new[] { "old" }, 24, 2, false);
                InterfacePropertyWriter.Apply(target, before, changes);
                Equal("24", target.State.Get(PropertyRules.SlotsNum));
                Equal(2, target.State.Version);
                Equal(1, target.Saves);
                True(!InterfaceEditRules.Changes(target.Read(), "", new[] { "old", "tip" }, 3, false)
                    .ContainsKey(PropertyRules.SlotsNum));
                Equal(0, InterfaceEditRules.Changes(target.Read(), "", new[] { "old" }, 24, 2, true).Count);
            });
            Run("Plugin initializes legacy managed versions and repairs an explicitly supplied interface list", delegate {
                var snapshot = InterfaceSnapshot();
                snapshot.Values[PropertyRules.AssetVersion] = "legacy";
                snapshot.Values[PropertyRules.AcceptsInterfaces] = "old；tip";
                Equal(1, snapshot.Version);
                Equal("1", InterfaceEditRules.VersionChanges(snapshot, 1)[PropertyRules.AssetVersion]);
                Equal("1", InterfaceEditRules.VersionChanges(snapshot, 1, PropertyRules.AssemblyVersion)[PropertyRules.AssemblyVersion]);
                var changes = InterfaceEditRules.Changes(snapshot, null, new[] { "old", "tip" }, null, 1, true);
                Equal("old;tip", changes[PropertyRules.AcceptsInterfaces]);
                Equal("1", changes[PropertyRules.AssetVersion]);
                True(!changes.ContainsKey(PropertyRules.ConnectionInterface));
            });
            Run("Changing the root assembly package version preserves published asset semantics", delegate {
                var published = new Dictionary<string, string>
                {
                    { PropertyRules.AssetVersion, "1" }, { PropertyRules.AssemblyVersion, "1" }, { PropertyRules.SlotsNum, "24" }
                };
                var current = new Dictionary<string, string>(published);
                current[PropertyRules.AssemblyVersion] = "2";
                Equal(0, ExportCoordinator.RegisteredPropertyDifferences(published, current).Count);
                current[PropertyRules.SlotsNum] = "16";
                True(ExportCoordinator.RegisteredPropertyDifferences(published, current).Single().StartsWith(PropertyRules.SlotsNum));
            });
            Run("Interface catalogue merges persistently and never overwrites corrupted input", delegate {
                var folder = Path.Combine(Path.GetTempPath(), "interface-catalog-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
                try { var path = Path.Combine(folder, "interfaces.json"); var store = new InterfaceCatalogStore(path);
                    Equal(0, store.Load().Count); store.Merge(new[] { "B", "A", "A" }); new InterfaceCatalogStore(path).Merge(new[] { "C" });
                    Equal("A;B;C", string.Join(";", store.Load())); File.WriteAllText(path, "damaged");
                    Throws<ValidationException>(() => store.Merge(new[] { "D" })); Equal("damaged", File.ReadAllText(path));
                } finally { Directory.Delete(folder, true); }
            });
            Run("Refreshing draft source versions does not mutate stored binding endpoints", delegate {
                var old = new BindingEndpoint { Context = "1", AssetId = "asset:1" }; var d = new BindingEditorState(); d.SetParent(old);
                d.RefreshEndpoints(e => new BindingEndpoint { Context = e.Context, AssetId = "asset:2" }); Equal("asset:1", old.AssetId); Equal("asset:2", d.Parent.AssetId);
            });
        }
        private sealed class InterfaceTargetFake : IInterfacePropertyTarget
        {
            public bool ReadOnly { get; set; } public bool Dirty { get; set; }
            public InterfacePropertySnapshot State = InterfaceSnapshot(); public int Writes, Saves, FailWrite; public bool FailSave;
            public InterfacePropertySnapshot Read()
            {
                var s = new InterfacePropertySnapshot(); foreach (var p in State.Raw) s.Raw.Add(p.Key, p.Value);
                foreach (var p in State.Values) s.Values.Add(p.Key, p.Value); foreach (var p in State.Types) s.Types.Add(p.Key, p.Value); s.Linked.UnionWith(State.Linked); return s;
            }
            public void Set(string name, string value, int type) { Writes++; if (Writes == FailWrite) throw new Exception("write failure"); State.Raw[name] = value; State.Values[name] = value; State.Types[name] = type; Dirty = true; }
            public void Delete(string name) { State.Raw.Remove(name); State.Values.Remove(name); State.Types.Remove(name); }
            public void Save() { Saves++; if (FailSave) throw new Exception("save failure"); Dirty = false; }
        }
    }
}
