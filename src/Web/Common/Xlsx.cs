using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Horizon.Web.Common;

// Classeur Excel (.xlsx) d'une seule feuille, équivalent de
// XLSX.utils.json_to_sheet + book_append_sheet + writeFile (SheetJS) utilisés
// par le front d'origine : une ligne d'en-tête (les libellés), puis une
// ligne par enregistrement ; texte en cellule texte, nombre en cellule
// numérique, booléen en cellule booléenne, null laissé vide. Écrit à la main
// (SpreadsheetML + ZIP) plutôt qu'avec une bibliothèque : quelques Ko au
// lieu de plusieurs Mo à télécharger dans le navigateur.
public static class Xlsx
{
    public static byte[] Write(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            Add(zip, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Add(zip, "xl/workbook.xml",
                $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{Escape(sheetName)}" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add(zip, "xl/_rels/workbook.xml.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            Add(zip, "xl/worksheets/sheet1.xml", Sheet(headers, rows));
        }
        return stream.ToArray();
    }

    private static string Sheet(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var data = new StringBuilder();
        var rowIndex = 1;
        AppendRow(data, rowIndex, headers.Cast<object?>().ToList());
        foreach (var row in rows) AppendRow(data, ++rowIndex, row);
        var dimension = headers.Count == 0 ? "A1" : $"A1:{ColumnName(headers.Count - 1)}{rowIndex}";
        return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="{dimension}"/><sheetData>{data}</sheetData></worksheet>""";
    }

    private static void AppendRow(StringBuilder data, int rowIndex, IReadOnlyList<object?> values)
    {
        data.Append($"<row r=\"{rowIndex}\">");
        for (var i = 0; i < values.Count; i++)
        {
            var reference = $"{ColumnName(i)}{rowIndex}";
            switch (values[i])
            {
                case null:
                    break;
                case string text:
                    data.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(text)}</t></is></c>");
                    break;
                case bool flag:
                    data.Append($"<c r=\"{reference}\" t=\"b\"><v>{(flag ? 1 : 0)}</v></c>");
                    break;
                case var number when JsValue.IsNumber(number) && double.IsFinite(JsValue.ToDouble(number)):
                    data.Append($"<c r=\"{reference}\"><v>{JsValue.ToDouble(number).ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    break;
                case var other:
                    data.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(JsValue.ToJsString(other))}</t></is></c>");
                    break;
            }
        }
        data.Append("</row>");
    }

    // 0 → A, 25 → Z, 26 → AA...
    private static string ColumnName(int index)
    {
        var name = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    // Caractères de contrôle interdits en XML retirés (sauf tabulation et
    // retours à la ligne).
    private static string Escape(string text) =>
        SecurityElement.Escape(new string([.. text.Where(c => c >= 0x20 || c is '\t' or '\n' or '\r')]))!;

    private static void Add(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path, CompressionLevel.Fastest).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
