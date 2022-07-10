﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserUnitTests;

public class UnitTestConfig
{

    [Fact]
    public void TestConfigFile()
    {
        var app = App.GetParserApp<Parser>("appsettings2.json");
        var options = app.Services.GetService<IOptions<TokenizerOptions>>().Value;

        Assert.Equal("2.0", options.Version);
    }


}
