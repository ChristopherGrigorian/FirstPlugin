using System.Collections.Generic;
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

    public void FilterBundle()
    {
        var escSheet = Plugin.DataManager.GetExcelSheet<EquipSlotCategory>();
        var cjcSheet = Plugin.DataManager.GetExcelSheet<ClassJobCategory>();

        int loopConstraint = 0;

        Bundle bundle = new Bundle();

        foreach (var item in Plugin.DataManager.GetExcelSheet<Item>())
        {
            // -- Filter --
            if (item.LevelEquip != 100)
            {
                continue;
            }

            if (item.FilterGroup != 4)
            {
                continue;
            }

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

            if (loopConstraint % 5 == 0)
            {
                bundle.Identifier = item.RowId;
            }
            bundle.ItemBundle.Add(item);
            if (loopConstraint % 5 == 4)
            {
                bundles.Add(bundle);
                bundle = new Bundle();
            }
            loopConstraint += 1;
        }
    }
}
