using ClientPortalProgressTracker.Models.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClientPortalProgressTracker.Services;

public class PortalPdfService
{
    public byte[] GenerateQuotePdf(
        Project project,
        Freelancer freelancer,
        List<Milestone> milestones,
        QuoteSignature? signature)
    {
        var brand = string.IsNullOrWhiteSpace(freelancer.BusinessName) ? freelancer.FullName : freelancer.BusinessName;
        var depositAmount = Math.Round(project.TotalAmount * project.DepositPercentage / 100m, 2);
        var balanceAmount = Math.Max(0, project.TotalAmount - depositAmount);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(brand).FontSize(18).Bold().FontColor(Colors.Indigo.Darken2);
                        if (!string.IsNullOrWhiteSpace(freelancer.Email))
                            col.Item().Text(freelancer.Email).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(180).AlignRight().Column(col =>
                    {
                        col.Item().Text(signature != null ? "SIGNED QUOTE" : "PROJECT QUOTE")
                            .FontSize(14).Bold().FontColor(signature != null ? Colors.Green.Darken2 : Colors.Grey.Darken3);
                        col.Item().Text($"Ref: {project.ReferenceCode}").FontSize(9);
                        if (project.QuoteExpiresAt.HasValue && signature == null)
                            col.Item().Text($"Valid until: {project.QuoteExpiresAt.Value:dd MMM yyyy}").FontSize(9);
                    });
                });

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Background(Colors.Grey.Lighten4).Padding(12).Column(info =>
                    {
                        info.Item().Text(project.Title).FontSize(14).Bold();
                        info.Item().Text($"Prepared for: {project.ClientName} ({project.ClientEmail})").FontSize(10);
                    });

                    if (!string.IsNullOrWhiteSpace(project.MessageToClient))
                    {
                        col.Item().BorderLeft(3).BorderColor(Colors.Indigo.Medium).PaddingLeft(10).Column(msg =>
                        {
                            msg.Item().Text("Note from freelancer").FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                            msg.Item().Text(project.MessageToClient).FontSize(10);
                        });
                    }

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(28);
                            columns.RelativeColumn();
                            columns.ConstantColumn(100);
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(5).Text("#").Bold();
                            header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(5).Text("Milestone & Scope").Bold();
                            header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(5).AlignRight().Text("Amount").Bold();
                        });

                        for (int i = 0; i < milestones.Count; i++)
                        {
                            var ms = milestones[i];
                            table.Cell().PaddingVertical(6).Text($"{i + 1}");
                            table.Cell().PaddingVertical(6).Column(c =>
                            {
                                c.Item().Text(ms.Title).Bold();
                                if (!string.IsNullOrWhiteSpace(ms.Description))
                                    c.Item().Text(ms.Description).FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                            table.Cell().PaddingVertical(6).AlignRight().Text($"R {(ms.Amount ?? 0m):N2}");
                        }
                    });

                    col.Item().AlignRight().Width(240).Column(totals =>
                    {
                        totals.Spacing(4);
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Total Project Value:");
                            r.ConstantItem(100).AlignRight().Text($"R {project.TotalAmount:N2}").Bold();
                        });
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text($"Deposit ({project.DepositPercentage}%):");
                            r.ConstantItem(100).AlignRight().Text($"R {depositAmount:N2}");
                        });
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Balance on Completion:");
                            r.ConstantItem(100).AlignRight().Text($"R {balanceAmount:N2}");
                        });
                    });

                    if (signature != null)
                    {
                        col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Green.Medium).Background(Colors.Green.Lighten5).Padding(12).Column(sig =>
                        {
                            sig.Spacing(3);
                            sig.Item().Text("DIGITAL SIGNATURE & AUDIT TRAIL").FontSize(9).Bold().FontColor(Colors.Green.Darken3);
                            sig.Item().Text($"Signed by: {signature.SignerFullName}").Bold();
                            sig.Item().Text($"Signed on: {signature.SignedAt:dd MMM yyyy, HH:mm} UTC");
                            if (!string.IsNullOrWhiteSpace(signature.IpAddress))
                                sig.Item().Text($"IP Address: {signature.IpAddress}").FontSize(8).FontColor(Colors.Grey.Darken2);
                            if (!string.IsNullOrWhiteSpace(signature.ScopeHash))
                                sig.Item().Text($"Scope Fingerprint (SHA-256): {signature.ScopeHash}").FontSize(7).FontColor(Colors.Grey.Darken1);
                        });
                    }
                });

                page.Footer().AlignCenter().Text("Generated by Client Portal Progress Tracker").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }
    public byte[] GenerateReceiptPdf(Project project, Freelancer freelancer, PaymentTransaction tx)
    {
        var brand = string.IsNullOrWhiteSpace(freelancer.BusinessName) ? freelancer.FullName : freelancer.BusinessName;
        var typeLabel = tx.PaymentType == PaymentType.Deposit ? "Project Deposit"
            : (tx.PaymentType == PaymentType.FinalBalance ? "Final Balance Payment" : "Full Project Payment");
        var methodLabel = tx.PaymentMethod == PaymentMethod.ManualEft ? "Manual EFT" : "Online Payment (Paystack)";
        var paidDate = tx.PaidAt ?? tx.VerifiedAt ?? tx.CreatedAt;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(brand).FontSize(18).Bold().FontColor(Colors.Indigo.Darken2);
                        if (!string.IsNullOrWhiteSpace(freelancer.Email))
                            col.Item().Text(freelancer.Email).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(180).AlignRight().Column(col =>
                    {
                        col.Item().Text("PAYMENT RECEIPT").FontSize(14).Bold().FontColor(Colors.Green.Darken2);
                        col.Item().Text($"Project Ref: {project.ReferenceCode}").FontSize(9);
                        col.Item().Text($"Date: {paidDate:dd MMM yyyy}").FontSize(9);
                    });
                });

                page.Content().PaddingVertical(24).Column(col =>
                {
                    col.Spacing(16);

                    col.Item().Background(Colors.Grey.Lighten4).Padding(14).Column(info =>
                    {
                        info.Spacing(4);
                        info.Item().Text($"Received from: {project.ClientName} ({project.ClientEmail})").Bold();
                        info.Item().Text($"Project: {project.Title}");
                        info.Item().Text($"Payment Description: {typeLabel}");
                        info.Item().Text($"Payment Method: {methodLabel}");
                        info.Item().Text($"Transaction ID: {tx.Id}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    col.Item().Border(1).BorderColor(Colors.Green.Medium).Background(Colors.Green.Lighten5).Padding(16).Row(row =>
                    {
                        row.RelativeItem().Text("Amount Paid (ZAR)").FontSize(12).Bold();
                        row.ConstantItem(140).AlignRight().Text($"R {tx.Amount:N2}").FontSize(16).Bold().FontColor(Colors.Green.Darken3);
                    });

                    var remaining = Math.Max(0, project.TotalAmount - project.AmountPaid);
                    col.Item().AlignRight().Width(240).Column(summary =>
                    {
                        summary.Spacing(4);
                        summary.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Total Quote Value:");
                            r.ConstantItem(100).AlignRight().Text($"R {project.TotalAmount:N2}");
                        });
                        summary.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Total Paid to Date:");
                            r.ConstantItem(100).AlignRight().Text($"R {project.AmountPaid:N2}").Bold();
                        });
                        summary.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Remaining Balance:");
                            r.ConstantItem(100).AlignRight().Text($"R {remaining:N2}");
                        });
                    });
                });

                page.Footer().AlignCenter().Text("Official Payment Receipt · Generated by Client Portal Progress Tracker").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }
}
