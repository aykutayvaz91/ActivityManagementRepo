using System;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using ActivityManagement.Reports.Dto;

namespace ActivityManagement.Web.Helpers
{
    // Rapor PDF üretici (MigraDoc/PDFsharp core, net8.0 + özel font çözücü). Excel export'larının PDF karşılığı.
    public static class ReportPdfBuilder
    {
        private const string FontName = "Arial";
        private static readonly object _lock = new object();
        private static bool _fontsReady;

        private static void EnsureFonts()
        {
            if (_fontsReady) return;
            lock (_lock)
            {
                if (_fontsReady) return;
                if (PdfSharp.Fonts.GlobalFontSettings.FontResolver == null)
                    PdfSharp.Fonts.GlobalFontSettings.FontResolver = PdfFontResolver.Instance;
                _fontsReady = true;
            }
        }

        public static byte[] BuildPersonal(PersonalReportDto r)
        {
            EnsureFonts();
            var doc = NewDoc($"Kişisel Rapor — {r.EmployeeName}");
            var sec = doc.LastSection;

            AddTitle(sec, "Kişisel Rapor");
            AddMeta(sec, "Personel", r.EmployeeName);
            AddMeta(sec, "Dönem", $"{r.StartDate:dd.MM.yyyy} - {r.EndDate:dd.MM.yyyy}");
            if (!string.IsNullOrWhiteSpace(r.SummaryText)) AddMeta(sec, "Özet", r.SummaryText);

            sec.AddParagraph().AddLineBreak();
            AddHeading(sec, "İş Tipi Dağılımı");
            var t1 = NewTable(sec, new[] { "İş Tipi", "Adet", "Saat" }, new[] { 8.0, 3.0, 3.0 });
            if (r.TaskTypeBreakdown != null)
                foreach (var b in r.TaskTypeBreakdown)
                    AddRow(t1, b.Type, b.Count.ToString(), b.Hours.ToString("0.##"));

            sec.AddParagraph().AddLineBreak();
            AddHeading(sec, "Günlük Faaliyet");
            var t2 = NewTable(sec, new[] { "Tarih", "Faaliyet", "Saat" }, new[] { 6.0, 4.0, 4.0 });
            if (r.DailyActivities != null)
                foreach (var d in r.DailyActivities)
                    AddRow(t2, d.Date.ToString("dd.MM.yyyy"), d.ActivityCount.ToString(), d.Hours.ToString("0.##"));

            return Render(doc);
        }

        public static byte[] BuildTeam(TeamReportDto r)
        {
            EnsureFonts();
            var doc = NewDoc("Ekip Raporu");
            var sec = doc.LastSection;

            AddTitle(sec, "Ekip Raporu");
            AddMeta(sec, "Dönem", $"{r.StartDate:dd.MM.yyyy} - {r.EndDate:dd.MM.yyyy}");
            sec.AddParagraph().AddLineBreak();

            var t = NewTable(sec,
                new[] { "Personel", "Departman", "Ünvan", "Saat", "Faaliyet", "Tamamlanan", "Bekleyen" },
                new[] { 4.5, 3.0, 3.0, 2.0, 2.0, 2.2, 2.0 });
            if (r.EmployeeSummaries != null)
                foreach (var e in r.EmployeeSummaries)
                    AddRow(t, e.FullName, e.Department, e.Title,
                        e.TotalHours.ToString("0.##"), e.TotalActivities.ToString(),
                        e.CompletedTasks.ToString(), e.PendingTasks.ToString());

            return Render(doc);
        }

        // --- yardımcılar ---
        private static Document NewDoc(string title)
        {
            var doc = new Document { Info = { Title = title } };
            var style = doc.Styles["Normal"];
            style.Font.Name = FontName;
            style.Font.Size = 9;
            var sec = doc.AddSection();
            sec.PageSetup.Orientation = Orientation.Landscape;
            sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
            sec.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
            sec.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
            sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.2);
            // Alt bilgi: oluşturulma zamanı + sayfa
            var footer = sec.Footers.Primary.AddParagraph();
            footer.Format.Font.Size = 7;
            footer.Format.Font.Color = Colors.Gray;
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.AddText("ActivityManagement · sayfa ");
            footer.AddPageField();
            return doc;
        }

        private static void AddTitle(Section sec, string text)
        {
            var p = sec.AddParagraph(text);
            p.Format.Font.Size = 16;
            p.Format.Font.Bold = true;
            p.Format.SpaceAfter = Unit.FromPoint(6);
        }

        private static void AddHeading(Section sec, string text)
        {
            var p = sec.AddParagraph(text);
            p.Format.Font.Size = 11;
            p.Format.Font.Bold = true;
            p.Format.SpaceAfter = Unit.FromPoint(3);
        }

        private static void AddMeta(Section sec, string label, string value)
        {
            var p = sec.AddParagraph();
            var b = p.AddFormattedText(label + ": ", TextFormat.Bold);
            p.AddText(value ?? "-");
            p.Format.SpaceAfter = Unit.FromPoint(2);
        }

        private static Table NewTable(Section sec, string[] headers, double[] widthsCm)
        {
            var table = sec.AddTable();
            table.Borders.Width = 0.5;
            table.Borders.Color = new Color(210, 210, 210);
            for (int i = 0; i < headers.Length; i++)
            {
                var c = table.AddColumn(Unit.FromCentimeter(widthsCm[i]));
                c.Format.Alignment = ParagraphAlignment.Left;
            }
            var hr = table.AddRow();
            hr.Shading.Color = new Color(235, 235, 235);
            hr.Format.Font.Bold = true;
            for (int i = 0; i < headers.Length; i++)
                hr.Cells[i].AddParagraph(headers[i]);
            return table;
        }

        private static void AddRow(Table table, params string[] cells)
        {
            var row = table.AddRow();
            for (int i = 0; i < cells.Length && i < table.Columns.Count; i++)
                row.Cells[i].AddParagraph(cells[i] ?? "");
        }

        private static byte[] Render(Document doc)
        {
            var renderer = new PdfDocumentRenderer { Document = doc };
            renderer.RenderDocument();
            using var ms = new System.IO.MemoryStream();
            renderer.PdfDocument.Save(ms, false);
            return ms.ToArray();
        }
    }
}
