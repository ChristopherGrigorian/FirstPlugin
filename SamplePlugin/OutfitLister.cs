using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;

public sealed class OutfitLister
{
    private readonly IDataManager data;

    public OutfitLister(IDataManager data) => this.data = data;

    public IEnumerable<Item> GetAllWearableItems()
    {
        var sheet = this.data.GetExcelSheet<Item>();
        if (sheet == null) return Enumerable.Empty<Item>();

        return sheet
            .Where(i =>
                i.RowId != 0 &&
                !string.IsNullOrWhiteSpace(i.Name.ToString()) &&
                i.EquipSlotCategory.RowId != 0
            );
    }
}
