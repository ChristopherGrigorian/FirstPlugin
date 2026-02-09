using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;


namespace SamplePlugin;

public sealed class BundleCreater
{
    private readonly IDataManager data;

    public BundleCreater(IDataManager data) => this.data = data;

    public List<Bundle> bundles = new List<Bundle>();

    public int MinLevel { get; set; } = 1;
    public int MaxLevel { get; set; } = 100;

    public void FilterBundle()
    {
        bundles.Clear();

        var escSheet = Plugin.DataManager.GetExcelSheet<EquipSlotCategory>();
        var cjcSheet = Plugin.DataManager.GetExcelSheet<ClassJobCategory>();

        int loopConstraint = 0;
        Bundle bundle = new Bundle();

        uint previousID = 0;
        bool havePrev = false;

        foreach (var item in Plugin.DataManager.GetExcelSheet<Item>())
        {
            // -- Filter --
            var lvl = (int)item.LevelEquip;
            if (lvl < MinLevel || lvl > MaxLevel)
                continue;

            if (item.FilterGroup != 4)
            {
                continue;
            }

            var name = item.Name.ToString();

            if (name.StartsWith("Ornate ", StringComparison.OrdinalIgnoreCase))
                continue;


            var escRowId = item.EquipSlotCategory.RowId;
            EquipSlotCategory? esc = null;
            if (escRowId != 0 && escSheet != null)
            {
                esc = escSheet.GetRow(escRowId);
            }

            if (esc != null)
            {
                var e = esc.Value;

                if (e.Ears == 1 || e.Neck == 1 || e.Wrists == 1 || e.FingerR == 1)
                {
                    continue;
                }
            }
            // -- End Of Filter --
            if (havePrev && item.RowId - previousID > 1)
            {
                if (bundle.ItemBundle.Count > 0)
                {
                    bundles.Add(bundle);
                }
                bundle = new Bundle();
                loopConstraint = 0;
            }

            if (loopConstraint == 0)
            {
                bundle.Identifier = item.RowId;
            }

            bundle.ItemBundle.Add(item);
            loopConstraint++;

            if (loopConstraint == 5)
            {
                bundles.Add(bundle);
                bundle = new Bundle();
                loopConstraint = 0;
            }
            
            previousID = item.RowId;
            havePrev = true;
        }

        if (bundle.ItemBundle.Count > 0)
        {
            bundles.Add(bundle);
        }

        var dir = Plugin.PluginInterface.AssemblyLocation.Directory!.FullName; // same folder as goat.png

        foreach (var file in Directory.GetFiles(dir, "*.jpeg"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file); // "42992;Archeo Kingdom Set of Casting"
            var parts = fileName.Split(';', 2);
            if (parts.Length != 2)
                continue;

            if (!uint.TryParse(parts[0], out var id))
                continue;

            var setName = parts[1].Trim();

            // find the bundle with that Identifier
            var target = bundles.FirstOrDefault(b => b.Identifier == id);
            if (target == null)
                continue;

            target.SetImagePath = file;
            target.GearSetName = setName;
        }
    }
}
