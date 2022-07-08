using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserTests;

public class Operator
{
#nullable disable
    public string Name { get; set; }
#nullable restore

    public int? Priority { get; set; } = 0;

    public bool LeftToRight { get; set; } = true;
}
