using System;
using System.Collections.Generic;
using System.Text;
using Lumina.Excel.Sheets;

namespace SamplePlugin;

public class Bundle {
    public uint Identifier;
    public string GearSetName = "";
    public List<Item> ItemBundle = new List<Item>();
    public string? SetImagePath;
}
