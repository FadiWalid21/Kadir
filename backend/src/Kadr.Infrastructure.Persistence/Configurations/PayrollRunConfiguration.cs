using Kadr.Platform.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.ToTable("payroll_runs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RulesSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.TotalGross).HasPrecision(18, 2);
        builder.Property(x => x.TotalNet).HasPrecision(18, 2);
        builder.Property(x => x.TotalEmployerCost).HasPrecision(18, 2);

        // PostgreSQL system column xmin as optimistic concurrency token.
        builder.Property(x => x.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => new { x.TenantId, x.PeriodStart, x.PeriodEnd });
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PeriodEnd });
        builder.HasIndex(x => new { x.TenantId, x.ReopenedFromRunId });

        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.IsImmutable);
    }
}

public sealed class PayrollEntryConfiguration : IEntityTypeConfiguration<PayrollEntry>
{
    public void Configure(EntityTypeBuilder<PayrollEntry> builder)
    {
        builder.ToTable("payroll_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SalarySnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.InputsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.WarningsJson).HasColumnType("jsonb");
        builder.Property(x => x.Gross).HasPrecision(18, 2);
        builder.Property(x => x.InsurableWage).HasPrecision(18, 2);
        builder.Property(x => x.EmployeeInsurance).HasPrecision(18, 2);
        builder.Property(x => x.EmployerInsurance).HasPrecision(18, 2);
        builder.Property(x => x.TaxableIncome).HasPrecision(18, 2);
        builder.Property(x => x.IncomeTax).HasPrecision(18, 2);
        builder.Property(x => x.OtherDeductions).HasPrecision(18, 2);
        builder.Property(x => x.Net).HasPrecision(18, 2);
        builder.Property(x => x.EmployerCostTotal).HasPrecision(18, 2);
        builder.Property(x => x.HoldReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.RunId, x.EmployeeId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.RunId, x.IsHeld });
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId });
        builder.HasIndex(x => new { x.TenantId, x.RunId, x.WarningCount });

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.EntryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines");

        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class PayrollEntryLineConfiguration : IEntityTypeConfiguration<PayrollEntryLine>
{
    public void Configure(EntityTypeBuilder<PayrollEntryLine> builder)
    {
        builder.ToTable("payroll_entry_lines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LineType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ComponentKey).HasMaxLength(64);
        builder.Property(x => x.LabelAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.LabelEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.CalcExplanationJson).HasColumnType("jsonb");

        builder.HasIndex(x => new { x.TenantId, x.EntryId, x.SortOrder });
        builder.HasIndex(x => new { x.TenantId, x.EntryId, x.LineType });
    }
}
