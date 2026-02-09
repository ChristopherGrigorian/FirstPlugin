using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Ipc.Exceptions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string goatImagePath;
    private readonly Plugin plugin;
    private readonly Dictionary<string, ISharedImmediateTexture> setImageCache = new();


    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin, string goatImagePath)
        : base("My Amazing Window##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.goatImagePath = goatImagePath;
        this.plugin = plugin;
    }

    private ISharedImmediateTexture? GetSetImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!setImageCache.TryGetValue(path, out var tex))
        {
            tex = Plugin.TextureProvider.GetFromFile(path);
            setImageCache[path] = tex;
        }

        return tex;
    }

    public void Dispose() 
    {
        setImageCache.Clear();
    }

    public override void Draw()
    {
        ImGui.Text($"The random config bool is {plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");

        if (ImGui.Button("Show Settings"))
        {
            plugin.ToggleConfigUi();
        }

        var bc = Plugin.BundleCreater;

        int min = bc.MinLevel;
        int max = bc.MaxLevel;

        if (ImGui.DragIntRange2("Item level", ref min, ref max, 1, 1, 100))
        {
            min = Math.Clamp(min, 1, 100);
            max = Math.Clamp(max, 1, 100);
            if (min > max) (min, max) = (max, min);

            bc.MinLevel = min;
            bc.MaxLevel = max;

            bc.FilterBundle();
        }



        ImGui.Spacing();

        // Normally a BeginChild() would have to be followed by an unconditional EndChild(),
        // ImRaii takes care of this after the scope ends.
        // This works for all ImGui functions that require specific handling, examples are BeginTable() or Indent().
        using (var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true))
        {
            // Check if this child is drawing
            if (child.Success)
            {
                ImGui.Text("Have a goat:");
                var goatImage = Plugin.TextureProvider.GetFromFile(goatImagePath).GetWrapOrDefault();
                if (goatImage != null)
                {
                    using (ImRaii.PushIndent(55f))
                    {
                        ImGui.Image(goatImage.Handle, goatImage.Size);
                    }
                }
                else
                {
                    ImGui.Text("Image not found.");
                }

                ImGuiHelpers.ScaledDummy(20.0f);

                // Example for other services that Dalamud provides.
                // PlayerState provides a wrapper filled with information about the player character.

                var playerState = Plugin.PlayerState;
                if (!playerState.IsLoaded)
                {
                    ImGui.Text("Our local player is currently not logged in.");
                    return;
                }
                
                if (!playerState.ClassJob.IsValid)
                {
                    ImGui.Text("Our current job is currently not valid.");
                    return;
                }

                // If you want to see the Macro representation of this SeString use `.ToMacroString()`
                // More info about SeStrings: https://dalamud.dev/plugin-development/sestring/
                ImGui.Text($"Our current job is ({playerState.ClassJob.RowId}) '{playerState.ClassJob.Value.Abbreviation}' with level {playerState.Level}");

                // Example for querying Lumina, getting the name of our current area.
                var territoryId = Plugin.ClientState.TerritoryType;
                if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
                {
                    ImGui.Text($"We are currently in ({territoryId}) '{territoryRow.PlaceName.Value.Name}'");
                }
                else
                {
                    ImGui.Text("Invalid territory.");
                }


                var bundles = Plugin.BundleCreater.bundles;

                ImGui.Text($"{bundles.Count} bundles");

                const int columns = 3;

                if (ImGui.BeginTable("BundleGrid", columns,
                    ImGuiTableFlags.SizingStretchSame |
                    ImGuiTableFlags.PadOuterX))
                {
                    for (int idx = 0; idx < bundles.Count; ++idx)
                    {
                        if (idx % columns == 0)
                        {
                            ImGui.TableNextRow();
                        }

                        ImGui.TableNextColumn();

                        var b = bundles[idx];

                        ImGui.PushID((int)b.Identifier);

                        ImGui.BeginChild("BundleCell", new Vector2(0, 800), true,
                            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

                        float imageW = 425f;
                        float padding = 8f;
                        float availW = ImGui.GetContentRegionAvail().X;
                        float leftW = Math.Max(0, availW - imageW - padding);

                        ImGui.BeginChild("Left", new Vector2(leftW, 0), false,
                            ImGuiWindowFlags.HorizontalScrollbar);

                        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(b.GearSetName)
                            ? $"Set {b.Identifier}"
                            : b.GearSetName);

                        ImGui.Separator();

                        int count = Math.Min(5, b.ItemBundle.Count);
                        for (int i = 0; i < count; i++)
                        {
                            var item = b.ItemBundle[i];
                            ImGui.PushID((int)item.RowId);

                            var lookup = new GameIconLookup { IconId = (uint)item.Icon };
                            var shared = Plugin.TextureProvider.GetFromGameIcon(lookup);

                            if (shared.TryGetWrap(out var wrap, out _) && wrap != null)
                            {
                                ImGui.Image(wrap.Handle, wrap.Size);
                                ImGui.SameLine();
                            }

                            if (ImGui.SmallButton("    "))
                            {
                                AgentTryon.TryOn(
                                    openerAddonId: 0,
                                    itemId: (uint)item.RowId,
                                    stain0Id: 0,
                                    stain1Id: 0,
                                    glamourItemId: 0,
                                    applyCompanyCrest: false
                                );
                            }
                            
                            ImGui.SameLine();

                            ImGui.TextUnformatted($"{item.Name}");

                            ImGui.PopID();
                        }

                        ImGui.EndChild();

                        ImGui.SameLine();

                        ImGui.BeginChild("Right", new Vector2(imageW, 800), false,
                            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

                        if (!string.IsNullOrWhiteSpace(b.SetImagePath))
                        {
                            var setTex = GetSetImage(b.SetImagePath!);
                            if (setTex != null && setTex.TryGetWrap(out var setWrap, out _) && setWrap != null)
                            {
                                float w = imageW;
                                float h = 800f;
                                ImGui.Image(setWrap.Handle, new Vector2(w, h));

                                var drawList = ImGui.GetWindowDrawList();
                                var minF = ImGui.GetItemRectMin();
                                var maxF = ImGui.GetItemRectMax();

                                drawList.AddRect(
                                    minF,
                                    maxF,
                                    ImGui.GetColorU32(ImGuiCol.Border),
                                    0f,                 // rounding
                                    ImDrawFlags.None,
                                    1.5f                // thickness
                                );

                            }
                            else
                            {
                                ImGui.TextUnformatted("Image Missing");
                            }
                        } else
                        {
                            ImGui.TextUnformatted("No set photo");
                        }

                        ImGui.EndChild();
                        ImGui.EndChild();
                        ImGui.PopID();
                    }

                    ImGui.EndTable();
                }
            }
        }
    }
}
