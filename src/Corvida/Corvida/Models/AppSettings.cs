using System;
using System.IO;

namespace Corvida.Models;

public class AppSettings
{
    public string DataPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CorvidaData");
}
