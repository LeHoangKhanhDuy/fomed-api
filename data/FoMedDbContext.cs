using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoMed.Api.Models;

public class FoMedContext : DbContext
{
    public FoMedContext(DbContextOptions<FoMedContext> options) : base(options) { }

    // ===== Catalogs / Services =====
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();

    // ===== Pharmacy =====
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<MedicineLot> MedicineLots => Set<MedicineLot>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<DispenseLine> DispenseLines => Set<DispenseLine>();

    // ===== Employee =====
    public DbSet<Employee> Employees { get; set; } = null!;

    // ===== Doctors =====
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorSpecialty> DoctorSpecialties => Set<DoctorSpecialty>();
    public DbSet<DoctorWeeklySlot> DoctorWeeklySlots => Set<DoctorWeeklySlot>();
    public DbSet<DoctorScheduleOverride> DoctorScheduleOverrides => Set<DoctorScheduleOverride>();
    public DbSet<DoctorEducation> DoctorEducations => Set<DoctorEducation>();
    public DbSet<DoctorExpertise> DoctorExpertises => Set<DoctorExpertise>();
    public DbSet<DoctorAchievement> DoctorAchievements => Set<DoctorAchievement>();
    public DbSet<DoctorRating> DoctorRatings => Set<DoctorRating>();

    // ===== Users & Auth =====
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<UserMfa> UserMfas => Set<UserMfa>();
    public DbSet<QrToken> QrTokens => Set<QrToken>();

    // ===== Patients & Visits =====
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<VisitQueue> VisitQueues => Set<VisitQueue>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<EncounterPrescription> EncounterPrescriptions => Set<EncounterPrescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();

    // ===== Labs =====
    public DbSet<LabTest> LabTests => Set<LabTest>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<LabOrderItem> LabOrderItems => Set<LabOrderItem>();
    public DbSet<EncounterLabTest> EncounterLabTests => Set<EncounterLabTest>();
    public DbSet<LabResult> LabResults => Set<LabResult>();

    // ===== Billing =====
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);

        // Converter cho LabStatus (LabOrders)
        var labStatusConverter = new ValueConverter<LabStatus, string>(
            v => v.ToString().ToLowerInvariant(),
            s => Enum.Parse<LabStatus>((s ?? "Processing"), true)
        );

        // ---------- Service ----------
        m.Entity<Service>(e =>
        {
            e.Property(x => x.BasePrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.ImageUrl).HasMaxLength(300);
            e.HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
            e.HasOne(s => s.Category).WithMany(c => c.Services)
             .HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<ServiceCategory>(e =>
        {
            e.Property(c => c.ImageUrl).HasMaxLength(300);
            e.HasIndex(c => c.Name).IsUnique();
            e.HasIndex(c => c.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        });

        // ---------- Pharmacy ----------
        m.Entity<Medicine>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
            e.HasIndex(x => x.Name);
            e.Property(x => x.BasePrice).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            e.Property(x => x.Unit).IsRequired();
        });

        m.Entity<MedicineLot>(e =>
        {
            e.Property(x => x.PurchasePrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            e.HasOne(x => x.Medicine).WithMany(x => x.Lots).HasForeignKey(x => x.MedicineId);
        });

        m.Entity<InventoryTransaction>(e =>
        {
            e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            e.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_InvTxn_TxnType", "TxnType IN ('in','out','adjust')");
                t.HasCheckConstraint("CK_InvTxn_QuantityNonZero", "Quantity <> 0");
            });
            e.HasOne(x => x.Medicine).WithMany(x => x.InventoryTransactions).HasForeignKey(x => x.MedicineId);
            e.HasOne(x => x.Lot).WithMany(x => x.InventoryTransactions).HasForeignKey(x => x.LotId);
            e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        });

        m.Entity<DispenseLine>(e =>
        {
            e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            e.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.PrescriptionItem).WithMany(x => x.DispenseLines).HasForeignKey(x => x.PrescriptionItemId);
            e.HasOne(x => x.Lot).WithMany(x => x.DispenseLines).HasForeignKey(x => x.LotId);
        });

        // ---------- Doctors ----------
        m.Entity<Doctor>(e =>
        {
            e.Property(x => x.RatingAvg).HasColumnType("decimal(3,2)");
            e.Property(x => x.LicenseNo).HasMaxLength(50);
            e.HasOne(x => x.PrimarySpecialty).WithMany().HasForeignKey(x => x.PrimarySpecialtyId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        m.Entity<DoctorSpecialty>(e =>
        {
            e.HasKey(x => new { x.DoctorId, x.SpecialtyId });
            e.HasOne(x => x.Doctor).WithMany(x => x.DoctorSpecialties).HasForeignKey(x => x.DoctorId);
            e.HasOne(x => x.Specialty).WithMany(x => x.DoctorSpecialties).HasForeignKey(x => x.SpecialtyId);
        });

        m.Entity<DoctorWeeklySlot>(e =>
        {
            e.HasOne(x => x.Doctor).WithMany(x => x.WeeklySlots).HasForeignKey(x => x.DoctorId);
        });

        m.Entity<DoctorEducation>(e =>
        {
            e.HasOne(x => x.Doctor).WithMany(x => x.Educations).HasForeignKey(x => x.DoctorId);
        });
        m.Entity<DoctorExpertise>(e =>
        {
            e.HasOne(x => x.Doctor).WithMany(x => x.Expertises).HasForeignKey(x => x.DoctorId);
        });
        m.Entity<DoctorAchievement>(e =>
        {
            e.HasOne(x => x.Doctor).WithMany(x => x.Achievements).HasForeignKey(x => x.DoctorId);
        });
        m.Entity<DoctorRating>(e =>
        {
            e.HasOne(x => x.Doctor).WithMany(x => x.Ratings).HasForeignKey(x => x.DoctorId);
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId);
            e.ToTable(t => t.HasCheckConstraint("CK_DoctorRatings_Score", "Score BETWEEN 1 AND 5"));
        });

        // ---------- Users & Auth ----------
        m.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.Phone);
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.HasOne(u => u.Profile)
            .WithOne(p => p.User)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        });

        m.Entity<UserProfile>(e =>
        {
            e.HasKey(p => p.UserId);
            e.Property(p => p.AvatarUrl).HasMaxLength(500);
        });

        m.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        m.Entity<UserSession>(e =>
        {
            e.HasOne(x => x.User).WithMany(x => x.Sessions).HasForeignKey(x => x.UserId);
        });

        m.Entity<UserExternalLogin>(e =>
        {
            e.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
            e.HasOne(x => x.User).WithMany(x => x.ExternalLogins).HasForeignKey(x => x.UserId);
        });

        m.Entity<UserMfa>(e =>
        {
            e.HasOne(x => x.User).WithOne(x => x.Mfa!).HasForeignKey<UserMfa>(x => x.UserId);
        });

        m.Entity<EmailVerificationToken>(e =>
        {
            e.HasKey(x => x.TokenId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        // ---------- Patients ----------
        m.Entity<Patient>(e =>
        {
            e.ToTable("Patients");
            e.HasIndex(x => x.FullName);
            e.HasIndex(x => x.Phone).IsUnique();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.Property(x => x.PatientCode)
            .HasComputedColumnSql("'BN' + RIGHT('0000' + CAST([PatientId] AS VARCHAR(4)), 4)", stored: false);
            e.HasIndex(p => p.PatientCode).IsUnique().HasFilter("[PatientCode] IS NOT NULL");
            e.Property(x => x.AllergyText).HasMaxLength(300);
        });

        // ---------- Appointments ----------
        m.Entity<Appointment>(e =>
        {
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);

            e.Property(x => x.Code).HasMaxLength(30);
            e.HasIndex(x => x.Code).HasFilter("[Code] IS NOT NULL");

            e.Property(x => x.Status).HasMaxLength(20);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Appointments_Status",
                    "Status IN ('waiting','booked','done','cancelled','no_show')");
            });

            // Tra cứu nhanh
            e.HasIndex(x => new { x.DoctorId, x.VisitDate, x.Status, x.VisitTime });

            // Chống trùng giờ cùng bác sĩ
            e.HasIndex(x => new { x.DoctorId, x.VisitDate, x.VisitTime })
             .IsUnique()
             .HasDatabaseName("UX_App_Doctor_Date_Time");

            // STT duy nhất theo (Bác sĩ + Ngày + QueueNo) - cho phép NULL
            e.HasIndex(x => new { x.DoctorId, x.VisitDate, x.QueueNo })
             .IsUnique()
             .HasFilter("[QueueNo] IS NOT NULL")
             .HasDatabaseName("UX_App_Doctor_Date_Queue");
        });

        m.Entity<VisitQueue>(e =>
        {
            e.HasKey(x => new { x.DoctorId, x.VisitDate, x.QueueNo });
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId);
            e.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId);
        });

        m.Entity<Encounter>(e =>
        {
            e.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId);
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);
        });

        m.Entity<EncounterPrescription>(e =>
        {
            e.HasOne(x => x.Encounter).WithMany(x => x.Prescriptions)
             .HasForeignKey(x => x.EncounterId);
            e.Property(x => x.Advice).HasMaxLength(1000);
        });

        m.Entity<PrescriptionItem>(e =>
        {
            e.Property(x => x.DoseText).HasMaxLength(100).IsRequired();
            e.Property(x => x.FrequencyText).HasMaxLength(100).IsRequired();
            e.Property(x => x.DurationText).HasMaxLength(100).IsRequired();
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasOne(x => x.Prescription).WithMany(x => x.Items).HasForeignKey(x => x.PrescriptionId);
            e.HasOne(x => x.Medicine).WithMany(x => x.PrescriptionItems).HasForeignKey(x => x.MedicineId);
        });

        // ---------- Lab Catalog ----------
        m.Entity<LabTest>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Code).HasMaxLength(50);
            e.HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        // ---------- Lab Orders ----------
        m.Entity<LabOrder>(e =>
        {
            e.ToTable("LabOrders");
            e.Property(x => x.Code).HasColumnType("varchar(20)").IsRequired();
            e.HasIndex(x => x.Code).IsUnique();

            e.Property(x => x.Status)
             .HasConversion(labStatusConverter)
             .HasColumnType("varchar(20)")
             .HasDefaultValue(LabStatus.Processing);

            e.Property(x => x.SampleType).HasMaxLength(100);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.Warning).HasMaxLength(1000);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)")
             .HasDefaultValueSql("SYSUTCDATETIME()");
        });

        m.Entity<LabOrderItem>(e =>
        {
            e.ToTable("LabOrderItems");
            e.Property(x => x.TestName).HasMaxLength(200).IsRequired();
            e.Property(x => x.ResultValue).HasMaxLength(100).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(50);
            e.Property(x => x.ReferenceText).HasMaxLength(200);
            e.Property(x => x.Note).HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)")
             .HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => new { x.LabOrderId, x.DisplayOrder, x.LabOrderItemId })
             .HasDatabaseName("IX_LabOrderItems_Order");
        });

        // ---------- EncounterLabTest & LabResult ----------
        m.Entity<EncounterLabTest>(e =>
        {
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_EncLab_Status", "Status IN ('ordered','done','cancelled')");
            });
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("ordered");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Encounter).WithMany(x => x.EncounterLabTests)
             .HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.LabTest).WithMany().HasForeignKey(x => x.LabTestId);
        });

        m.Entity<LabResult>(e =>
        {
            e.Property(x => x.ResultStatus).HasMaxLength(20).HasDefaultValue("normal");
            e.Property(x => x.ResultAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.EncLabTest).WithMany(x => x.LabResults)
             .HasForeignKey(x => x.EncLabTestId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.EncLabTestId, x.ResultAt });
        });

        // ---------- Billing ----------
        m.Entity<Invoice>(e =>
        {
            e.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(x => x.DiscountAmt).HasColumnType("decimal(18,2)");
            e.Property(x => x.TaxAmt).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.Code).IsUnique();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Invoices_Status",
                    "Status IN ('unpaid','partial','paid','cancelled')");
            });
        });

        m.Entity<InvoiceItem>(e =>
        {
            e.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_InvoiceItem_Type",
                    "ItemType IN ('visit','service','lab','medicine','other')");
            });
        });

        m.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // InventoryTransactions timestamps
        foreach (var e in ChangeTracker.Entries<InventoryTransaction>())
        {
            if (e.State == EntityState.Added)
            {
                e.Entity.CreatedAt = now;
                e.Entity.UpdatedAt = now;
            }
            else if (e.State == EntityState.Modified)
            {
                e.Entity.UpdatedAt = now;
            }
        }

        // Appointments timestamps
        foreach (var e in ChangeTracker.Entries<Appointment>())
        {
            if (e.State == EntityState.Added)
            {
                e.Entity.CreatedAt = now;
                e.Entity.UpdatedAt = now;
            }
            else if (e.State == EntityState.Modified)
            {
                e.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
