using Microsoft.EntityFrameworkCore;
namespace InstituteLMS.API;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Trainer> Trainers => Set<Trainer>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Admission> Admissions => Set<Admission>();
    public DbSet<FeePayment> FeePayments => Set<FeePayment>();
    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Exam> Exams => Set<Exam>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e => {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("UserId");
            e.Property(x => x.FullName).HasColumnName("FullName").HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasColumnName("Email").HasMaxLength(256).IsRequired();
            e.Property(x => x.Phone).HasColumnName("Phone").HasMaxLength(30).HasDefaultValue("").IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("PasswordHash").IsRequired();
            e.Property(x => x.Role).HasColumnName("Role").HasMaxLength(40).IsRequired();
            e.Property(x => x.BranchId).HasColumnName("BranchId");
            e.Property(x => x.IsActive).HasColumnName("IsActive").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("CreatedAt").IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });
        b.Entity<Branch>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Student>().HasIndex(x => x.AdmissionNo).IsUnique();
        b.Entity<FeePayment>().HasIndex(x => x.ReceiptNo).IsUnique();
        b.Entity<Course>().Property(x => x.Fees).HasPrecision(12, 2);
        b.Entity<FeePayment>().Property(x => x.Amount).HasPrecision(12, 2);
        b.Entity<Exam>().Property(x => x.TotalMarks).HasPrecision(8, 2);
    }
}
