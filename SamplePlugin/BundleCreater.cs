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

    public string DataDir => Plugin.PluginInterface.AssemblyLocation.Directory!.FullName;
    public string ManualCsvPath => Path.Combine(DataDir, "manual_sets.csv");
    public string BanlistPath => Path.Combine(DataDir, "banlist_sets.csv");
    private HashSet<uint> bannedItemIds = new();

    public int MinLevel { get; set; } = 1;
    public int MaxLevel { get; set; } = 100;

    public string SearchText { get; set; } = "";

    public void FilterBundle()
    {
        bundles.Clear();
        LoadBanlist();

        var claimed = new HashSet<uint>();

        // 1) Load manual bundles
        var manual = LoadManualBundles();

        // 2) Only add manual bundles that pass filters, and ONLY THEN claim their items
        foreach (var b in manual)
        {
            foreach (var it in b.ItemBundle)
                claimed.Add(it.RowId);

            if (!BundlePassesFilters(b))
                continue;

            bundles.Add(b);
        }

        var escSheet = Plugin.DataManager.GetExcelSheet<EquipSlotCategory>();
        var cjcSheet = Plugin.DataManager.GetExcelSheet<ClassJobCategory>();

        int loopConstraint = 0;
        Bundle bundle = new Bundle();

        uint previousID = 0;
        bool havePrev = false;

        foreach (var item in Plugin.DataManager.GetExcelSheet<Item>())
        {
            // -- Filter --
            if (bannedItemIds.Contains(item.RowId))
                continue;

            var lvl = (int)item.LevelEquip;
            if (lvl < MinLevel || lvl > MaxLevel)
                continue;

            if (claimed.Contains(item.RowId))
                continue;

            if (item.FilterGroup != 4)
            {
                continue;
            }

            var name = item.Name.ToString();

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

            var cjcRowId = item.ClassJobCategory.RowId;
            ClassJobCategory? cjc = null;
            if (cjcRowId != 0 && cjcSheet != null)
            {
                cjc = cjcSheet.GetRow(cjcRowId);
            }

            if (cjc != null)
            {
                var c = cjc.Value;

                if (c.ADV && item.LevelEquip > 1)
                {
                    continue;
                }
            }

            // -- End Of Filter --
            if (havePrev && item.RowId - previousID > 1)
            {
                if (bundle.ItemBundle.Count > 0 && BundlePassesFilters(bundle))
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
                if (BundlePassesFilters(bundle))
                    bundles.Add(bundle);
                bundle = new Bundle();
                loopConstraint = 0;
            }
            
            previousID = item.RowId;
            havePrev = true;
        }

        if (bundle.ItemBundle.Count > 0 && BundlePassesFilters(bundle))
            bundles.Add(bundle);

        bundles = bundles
            .OrderBy(b => BundleMinLevel(b))
            .ThenBy(b => b.GearSetName ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.Identifier)
            .ToList();


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

    private List<Bundle> LoadManualBundles()
    {
        var result = new List<Bundle>();

        if (!File.Exists(ManualCsvPath))
        {
            Plugin.Log.Warning($"[BundleCreater] manual_sets.csv not found: {ManualCsvPath}");
            return result;
        }

        var itemSheet = Plugin.DataManager.GetExcelSheet<Item>();
        if (itemSheet == null) return result;

        foreach (var line in File.ReadAllLines(ManualCsvPath).Skip(1)) // skip header
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',', 4);
            if (parts.Length < 4) continue;

            if (!uint.TryParse(parts[0].Trim(), out var setId))
                continue;

            var setName = parts[1].Trim();
            var imageFile = parts[2].Trim();
            var itemIdsRaw = parts[3].Trim().Trim('"');

            var b = new Bundle
            {
                Identifier = setId,
                GearSetName = setName,
                SetImagePath = string.IsNullOrWhiteSpace(imageFile)
                    ? null
                    : Path.Combine(DataDir, imageFile)
            };

            foreach (var tok in itemIdsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!uint.TryParse(tok.Trim(), out var id))
                    continue;

                if (itemSheet.TryGetRow(id, out var it))
                {
                    b.ItemBundle.Add(it);
                }
            }

            if (b.ItemBundle.Count > 0)
                result.Add(b);
        }

        Plugin.Log.Information($"[BundleCreater] Loaded {result.Count} manual bundles from CSV.");
        return result;
    }

    private void LoadBanlist()
    {
        bannedItemIds.Clear();

        if (!File.Exists(BanlistPath))
        {
            Plugin.Log.Information("[BundleCreater] No banlist.csv found");
            return;
        }

        foreach (var line in File.ReadAllLines(BanlistPath).Skip(1)) // skip header
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (uint.TryParse(line.Trim(), out var id))
                bannedItemIds.Add(id);
        }

        Plugin.Log.Information($"[BundleCreater] Loaded {bannedItemIds.Count} banned item IDs");
    }


    private bool BundlePassesFilters(Bundle b)
    {
        // Level filter: require at least ONE item in range (or ALL—see note below)
        bool levelOk = b.ItemBundle.Any(it =>
        {
            var lvl = (int)it.LevelEquip;
            return lvl >= MinLevel && lvl <= MaxLevel;
        });

        if (!levelOk)
            return false;

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText;

            bool searchOk =
                (!string.IsNullOrWhiteSpace(b.GearSetName) &&
                 b.GearSetName.Contains(s, StringComparison.OrdinalIgnoreCase))
                || b.ItemBundle.Any(it =>
                    it.Name.ToString().Contains(s, StringComparison.OrdinalIgnoreCase));

            if (!searchOk)
                return false;
        }

        return true;
    }
    private static int BundleMinLevel(Bundle b)
    {
        // If empty, push to bottom
        return b.ItemBundle.Count == 0 ? int.MaxValue : b.ItemBundle.Min(it => (int)it.LevelEquip);
    }

}
