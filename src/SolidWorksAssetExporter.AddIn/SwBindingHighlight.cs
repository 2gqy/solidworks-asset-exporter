using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksAssetExporter.AddIn
{
    internal static class SwBindingHighlight
    {
        internal const int AssetMark = 1 << 29;
        internal const int ParentMark = 1 << 28;
        internal const int ChildMark = 1 << 27;

        internal static bool IsAutomaticMark(int mark)
        {
            return mark == AssetMark || mark == ParentMark || mark == ChildMark;
        }

        internal static int AutomaticCount(SelectionMgr selection)
        {
            return selection.GetSelectedObjectCount2(AssetMark) + selection.GetSelectedObjectCount2(ParentMark) +
                selection.GetSelectedObjectCount2(ChildMark);
        }

        internal static bool HasSelection(SelectionMgr selection) { return AutomaticCount(selection) > 0; }

        internal static void Clear(SelectionMgr selection)
        {
            var marked = Enumerable.Range(1, selection.GetSelectedObjectCount2(-1))
                .Where(index => IsAutomaticMark(selection.GetSelectedObjectMark(index))).ToArray();
            if (marked.Length != 0) selection.DeSelect2(marked, -1);
        }

        internal static int SelectMany(ModelDoc2 document, SelectionMgr selection, IEnumerable<Component2> components,
            int mark, bool append = true)
        {
            var requested = components.Where(component => component != null).ToList();
            if (requested.Count == 0) return 0;
            if (!append) document.ClearSelection2(true);

            var before = selection.GetSelectedObjectCount2(mark);
            var data = selection.CreateSelectData();
            data.Mark = mark;
            try
            {
                document.Extension.MultiSelect2(requested.Select(component => new DispatchWrapper(component)).ToArray(), true, data);
            }
            catch (COMException) { }
            if (selection.GetSelectedObjectCount2(mark) - before == requested.Count) return requested.Count;

            // SelectByID2 can toggle an object that was already selected. Only retry missing occurrences.
            var selectedNames = new HashSet<string>(StringComparer.Ordinal);
            var count = selection.GetSelectedObjectCount2(-1);
            for (var index = 1; index <= count; index++)
            {
                var component = selection.GetSelectedObject6(index, -1) as Component2;
                if (component != null) selectedNames.Add(component.Name2);
            }
            foreach (var component in requested)
            {
                if (!selectedNames.Add(component.Name2)) continue;
                SelectComponent(document, component, data);
            }
            return selection.GetSelectedObjectCount2(mark) - before;
        }

        private static void SelectComponent(ModelDoc2 document, Component2 component, SelectData data)
        {
            try
            {
                var name = component.GetSelectByIDString();
                if (!string.IsNullOrEmpty(name) && document.Extension.SelectByID2(name, "COMPONENT", 0, 0, 0,
                    true, data.Mark, null, 0)) return;
            }
            catch (COMException) { }
            try { component.Select4(true, data, false); }
            catch (COMException) { }
        }

        internal static void ColorSelections(SelectionMgr selection)
        {
            foreach (var mark in new[] { AssetMark, ParentMark, ChildMark })
                selection.set_SelectionColor(mark, ColorPreference(mark));
        }

        internal static Color GetRoleColor(SldWorks application, int mark)
        {
            return ColorTranslator.FromWin32(application.GetUserPreferenceIntegerValue(ColorPreference(mark)));
        }

        private static int ColorPreference(int mark)
        {
            switch (mark)
            {
                case AssetMark: return (int)swUserPreferenceIntegerValue_e.swSystemColorsSelectedItem1;
                case ParentMark: return (int)swUserPreferenceIntegerValue_e.swSystemColorsSelectedItem2;
                case ChildMark: return (int)swUserPreferenceIntegerValue_e.swSystemColorsSelectedItem3;
                default: throw new ArgumentOutOfRangeException("mark");
            }
        }
    }
}
