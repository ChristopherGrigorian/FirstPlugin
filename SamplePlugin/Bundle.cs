using System;
using System.Collections.Generic;
using System.Text;
using Lumina.Excel.Sheets;

namespace SamplePlugin;

public class Bundle {
    public uint Identifier;
    public List<Item> ItemBundle = new List<Item>();
}
