using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Utils;

public static class CSVUtils
{
    public static List<string[]> ReadCSV(string text, char sep = ',')
    {
        List<string[]> list = new List<string[]>();

        var text_lines = text.Replace("\r\n", "\n").Split('\n');

        var title_len = text_lines[0].Split(sep).Length;
        foreach (var line in text_lines)
        {
            var splited_line = line.Split(sep);
            if (splited_line.Length == title_len)
            {
                list.Add(splited_line);
            }
            else if (splited_line.Length < title_len)
            {
                var new_splited_line = new string[title_len];
                splited_line.CopyTo(new_splited_line, 0);
                for (int i = splited_line.Length; i < title_len; i++)
                {
                    new_splited_line[i] = "";
                }

                list.Add(new_splited_line);
            }
            else
            {
                list.Add(splited_line.Take(title_len).ToArray());
            }
        }

        return list;
    }
}