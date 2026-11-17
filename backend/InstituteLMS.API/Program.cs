using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using InstituteLMS.API;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Database connection is missing. Set ConnectionStrings__DefaultConnection in the hosting environment.");
if (builder.Environment.IsProduction())
{
    var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    if (configuredOrigins.Length == 0 || configuredOrigins.Any(x => !Uri.TryCreate(x, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        throw new InvalidOperationException("Production requires Cors__AllowedOrigins__0... to contain the exact HTTPS frontend origin(s).");
}
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Set Jwt__Key in environment variables (or Development appsettings for local testing).");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");
if (builder.Environment.IsProduction() && jwtKey.Contains("LOCAL-DEVELOPMENT", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("A private production Jwt__Key must be configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters {
    ValidateIssuer=true, ValidIssuer=builder.Configuration["Jwt:Issuer"], ValidateAudience=true, ValidAudience=builder.Configuration["Jwt:Audience"],
    ValidateIssuerSigningKey=true, IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ValidateLifetime=true, ClockSkew=TimeSpan.FromSeconds(30)
});
builder.Services.AddAuthorization();
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:4200", "http://localhost:5173" };
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context => {
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(error, "Unhandled API exception for {Path}", context.Request.Path);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { message = "The server encountered an error. Check the backend console log for details." });
}));
app.UseCors("frontend"); app.UseAuthentication();
app.Use(async (context, next) => {
    if (context.User.Identity?.IsAuthenticated == true) {
        var userIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tokenVersionText = context.User.FindFirstValue("authVersion");
        if (!int.TryParse(userIdText, out var userId) || !int.TryParse(tokenVersionText, out var tokenVersion)) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
        var db = context.RequestServices.GetRequiredService<AppDbContext>();
        var security = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => new { x.IsActive, x.AuthVersion }).SingleOrDefaultAsync();
        if (security is null || !security.IsActive || security.AuthVersion != tokenVersion) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
    }
    var mustChange = context.User.FindFirst("mustChangePassword")?.Value == "true";
    var allowed = context.Request.Path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.Equals("/api/health", StringComparison.OrdinalIgnoreCase);
    if (context.User.Identity?.IsAuthenticated == true && mustChange && !allowed) {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { message = "Password change required before continuing.", code = "PASSWORD_CHANGE_REQUIRED" });
        return;
    }
    await next();
});
app.UseAuthorization();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapGet("/api/health", () => Results.Ok(new { status="ok", service="Institute LMS API" }));
app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db, IPasswordHasher<AppUser> hasher, IConfiguration cfg) => {
    var email = req.Email?.Trim().ToLowerInvariant();
    var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
    if (user is null || !user.IsActive) return Results.Unauthorized();
    if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
        return Results.Json(new { message = "Account temporarily locked. Try again later or contact your administrator." }, statusCode: 423);
    if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password ?? "") == PasswordVerificationResult.Failed) {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= 5) { user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15); user.FailedLoginCount = 0; }
        await db.SaveChangesAsync();
        return Results.Unauthorized();
    }
    user.FailedLoginCount = 0; user.LockoutEndUtc = null; user.LastLoginAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    var claims = new[] { new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()), new Claim(ClaimTypes.Name,user.FullName), new Claim(ClaimTypes.Email,user.Email), new Claim(ClaimTypes.Role,user.Role), new Claim("branchId",user.BranchId?.ToString() ?? ""), new Claim("mustChangePassword",user.MustChangePassword ? "true" : "false"), new Claim("authVersion",user.AuthVersion.ToString()) };
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
    var token = new JwtSecurityToken(cfg["Jwt:Issuer"],cfg["Jwt:Audience"],claims,expires:DateTime.UtcNow.AddHours(8),signingCredentials:new SigningCredentials(key,SecurityAlgorithms.HmacSha256));
    return Results.Ok(new { token=new JwtSecurityTokenHandler().WriteToken(token), mustChangePassword=user.MustChangePassword, user=new { id=user.Id,name=user.FullName,email=user.Email,role=user.Role,branchId=user.BranchId } });
});
app.MapGet("/api/auth/me", (ClaimsPrincipal u) => Results.Ok(new { user=new { id=u.FindFirstValue(ClaimTypes.NameIdentifier),name=u.Identity?.Name,email=u.FindFirstValue(ClaimTypes.Email),role=u.FindFirstValue(ClaimTypes.Role),branchId=u.FindFirstValue("branchId") } })).RequireAuthorization();
app.MapPost("/api/auth/change-password", async (ChangePasswordRequest req, ClaimsPrincipal principal, AppDbContext db, IPasswordHasher<AppUser> hasher) => {
    if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Results.Unauthorized();
    var user = await db.Users.FindAsync(id); if (user is null || !user.IsActive) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 12) return Results.BadRequest(new { message = "New password must be at least 12 characters." });
    if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword ?? "") == PasswordVerificationResult.Failed) return Results.BadRequest(new { message = "Current password is incorrect." });
    user.PasswordHash = hasher.HashPassword(user, req.NewPassword); user.MustChangePassword = false; user.FailedLoginCount = 0; user.LockoutEndUtc = null; user.AuthVersion++;
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Password changed. Please sign in again." });
}).RequireAuthorization();

app.MapPost("/api/auth/seed-admin", async (SeedAdminRequest req, AppDbContext db, IPasswordHasher<AppUser> hasher, IConfiguration cfg) => {
    var setupKey=cfg["AdminSetupKey"];
    if (string.IsNullOrWhiteSpace(setupKey) || req.SetupKey != setupKey) return Results.NotFound();
    if (await db.Users.AnyAsync(x=>x.Role=="BusinessHead")) return Results.Conflict(new { message="Business Head already exists." });
    if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 12) return Results.BadRequest(new { message="Name, email and a password of at least 12 characters are required." });
    var email=req.Email.Trim().ToLowerInvariant(); if(await db.Users.AnyAsync(x=>x.Email==email)) return Results.Conflict(new { message="Email already exists." });
    var user=new AppUser { FullName=req.FullName.Trim(), Email=email, Phone=req.Phone?.Trim() ?? "", Role="BusinessHead", IsActive=true, MustChangePassword=false }; user.PasswordHash=hasher.HashPassword(user,req.Password); db.Users.Add(user); await db.SaveChangesAsync();
    return Results.Created("/api/auth/me",new { id=user.Id,email=user.Email });
});
// Local development recovery only. This endpoint is not registered in Production.
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/auth/dev-reset-business-head", async (DevResetRequest req, AppDbContext db, IPasswordHasher<AppUser> hasher, IConfiguration cfg) =>
    {
        var setupKey = cfg["AdminSetupKey"];
        if (string.IsNullOrWhiteSpace(setupKey) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(req.SetupKey ?? ""), Encoding.UTF8.GetBytes(setupKey)))
            return Results.NotFound();
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 12)
            return Results.BadRequest(new { message = "Use a password of at least 12 characters." });
        var admin = await db.Users.SingleOrDefaultAsync(x => x.Role == "BusinessHead" && x.Email == req.Email.Trim().ToLower());
        if (admin is null) return Results.NotFound(new { message = "Business Head account was not found." });
        admin.PasswordHash = hasher.HashPassword(admin, req.Password);
        admin.MustChangePassword = false;
        admin.FailedLoginCount = 0;
        admin.LockoutEndUtc = null;
        admin.AuthVersion++;
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "Business Head password reset. Existing account ID and profile were preserved." });
    });
}

// User administration is Business Head-only. Passwords are never returned.
app.MapGet("/api/users", async (AppDbContext db, ClaimsPrincipal u) => {
    if (u.FindFirstValue(ClaimTypes.Role)!="BusinessHead") return Results.Forbid();
    return Results.Ok(await db.Users.AsNoTracking().OrderBy(x=>x.FullName).Select(x=>new { x.Id,x.FullName,x.Email,x.Role,x.BranchId,x.IsActive,x.MustChangePassword,x.LastLoginAt,x.LockoutEndUtc,x.CreatedAt }).ToListAsync());
}).RequireAuthorization();
app.MapPost("/api/users", async (CreateUserRequest req, AppDbContext db, IPasswordHasher<AppUser> hasher, ClaimsPrincipal u) => {
    if (u.FindFirstValue(ClaimTypes.Role)!="BusinessHead") return Results.Forbid();
    var roles=new[]{"Manager","Trainer","Student"}; var email=req.Email?.Trim().ToLowerInvariant();
    if(string.IsNullOrWhiteSpace(req.FullName)||string.IsNullOrWhiteSpace(email)||!roles.Contains(req.Role)) return Results.BadRequest(new { message="Provide name, valid email and Manager/Trainer/Student role." });
    if(req.Role=="Manager" && !req.BranchId.HasValue) return Results.BadRequest(new { message="A Manager must be assigned to a branch." });
    if(await db.Users.AnyAsync(x=>x.Email==email)) return Results.Conflict(new { message="Email already exists." });
    if(req.BranchId.HasValue && !await db.Branches.AnyAsync(x=>x.Id==req.BranchId.Value && x.IsActive)) return Results.BadRequest(new { message="Selected branch does not exist or is inactive." });
    var temporaryPassword = GenerateTemporaryPassword();
    var user=new AppUser { FullName=req.FullName.Trim(),Email=email,Role=req.Role,BranchId=req.BranchId,IsActive=true,MustChangePassword=true };
    user.PasswordHash=hasher.HashPassword(user,temporaryPassword); db.Users.Add(user); await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", new { user.Id,user.FullName,user.Email,user.Role,user.BranchId,user.IsActive,user.MustChangePassword,user.CreatedAt,temporaryPassword });
}).RequireAuthorization();
app.MapPut("/api/users/{id:int}", async (int id, UpdateUserRequest req, AppDbContext db, ClaimsPrincipal u) => {
    if (u.FindFirstValue(ClaimTypes.Role)!="BusinessHead") return Results.Forbid();
    var user=await db.Users.FindAsync(id); if(user is null) return Results.NotFound();
    var email=req.Email?.Trim().ToLowerInvariant(); if(string.IsNullOrWhiteSpace(req.FullName)||string.IsNullOrWhiteSpace(email)||!new[]{"Manager","Trainer","Student"}.Contains(req.Role)) return Results.BadRequest(new { message="Invalid user details." });
    if(await db.Users.AnyAsync(x=>x.Id!=id && x.Email==email)) return Results.Conflict(new { message="Email already exists." });
    if(req.Role=="Manager" && !req.BranchId.HasValue) return Results.BadRequest(new { message="A Manager must be assigned to a branch." });
    if(req.BranchId.HasValue && !await db.Branches.AnyAsync(x=>x.Id==req.BranchId.Value && x.IsActive)) return Results.BadRequest(new { message="Selected branch does not exist or is inactive." });
    user.FullName=req.FullName.Trim(); user.Email=email; user.Role=req.Role; user.BranchId=req.BranchId; await db.SaveChangesAsync();
    return Results.Ok(new { user.Id,user.FullName,user.Email,user.Role,user.BranchId,user.IsActive,user.MustChangePassword,user.LastLoginAt,user.LockoutEndUtc,user.CreatedAt });
}).RequireAuthorization();
app.MapPatch("/api/users/{id:int}/status", async (int id, StatusRequest req, AppDbContext db, ClaimsPrincipal u) => {
    if (u.FindFirstValue(ClaimTypes.Role)!="BusinessHead") return Results.Forbid();
    var user=await db.Users.FindAsync(id); if(user is null) return Results.NotFound();
    if(user.Id.ToString()==u.FindFirstValue(ClaimTypes.NameIdentifier)) return Results.BadRequest(new { message="You cannot deactivate your own account." });
    user.IsActive=req.IsActive; user.AuthVersion++; if(req.IsActive){user.FailedLoginCount=0;user.LockoutEndUtc=null;} await db.SaveChangesAsync(); return Results.Ok(new { user.Id,user.IsActive });
}).RequireAuthorization();
app.MapPost("/api/users/{id:int}/reset-password", async (int id, AppDbContext db, IPasswordHasher<AppUser> hasher, ClaimsPrincipal u) => {
    if (u.FindFirstValue(ClaimTypes.Role)!="BusinessHead") return Results.Forbid();
    var user=await db.Users.FindAsync(id); if(user is null) return Results.NotFound();
    if(user.Id.ToString()==u.FindFirstValue(ClaimTypes.NameIdentifier)) return Results.BadRequest(new { message="Use the change-password page to change your own password." });
    var temporaryPassword=GenerateTemporaryPassword(); user.PasswordHash=hasher.HashPassword(user,temporaryPassword); user.MustChangePassword=true; user.AuthVersion++; user.FailedLoginCount=0; user.LockoutEndUtc=null; await db.SaveChangesAsync();
    return Results.Ok(new { message="Temporary password generated. It is shown only once.", temporaryPassword });
}).RequireAuthorization();
app.MapGet("/api/manager/dashboard", async (AppDbContext db, ClaimsPrincipal u) => {
    if(u.FindFirstValue(ClaimTypes.Role)!="Manager") return Results.Forbid();
    if(!int.TryParse(u.FindFirstValue("branchId"),out var branchId)) return Results.BadRequest(new { message="Manager has no assigned branch." });
    var branch=await db.Branches.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==branchId && x.IsActive); if(branch is null) return Results.NotFound(new { message="Assigned branch was not found or is inactive." });
    var studentIds=db.Students.Where(x=>x.BranchId==branchId).Select(x=>x.Id);
    var trainerCount=await db.Trainers.CountAsync(x=>x.BranchId==branchId && x.IsActive);
    var attendance=await db.Attendance.Where(x=>studentIds.Contains(x.StudentId) && x.Date==DateOnly.FromDateTime(DateTime.UtcNow)).ToListAsync();
    var present=attendance.Count(x=>x.Status.ToLower()=="present");
    var fees=await db.FeePayments.Where(x=>studentIds.Contains(x.StudentId)).SumAsync(x=>(decimal?)x.Amount) ?? 0m;
    var admissions=await db.Admissions.AsNoTracking().Where(x=>x.BranchId==branchId).OrderByDescending(x=>x.CreatedAt).Take(8).Select(x=>new { name=x.ApplicantName,course=x.Course,date=x.CreatedAt,status=x.Status }).ToListAsync();
    return Results.Ok(new { branch=new { branch.Id,branch.Name,branch.Code },totalStudents=await db.Students.CountAsync(x=>x.BranchId==branchId),activeTrainers=trainerCount,attendancePercent=attendance.Count==0?0:Math.Round((decimal)present/attendance.Count*100,1),presentCount=present,todayAttendanceCount=attendance.Count,feeCollection=fees,recentAdmissions=admissions });
}).RequireAuthorization();
app.MapGet("/api/{resource}", async (string resource, AppDbContext db, ClaimsPrincipal u) => {
    var role=u.FindFirstValue(ClaimTypes.Role); int? branch=int.TryParse(u.FindFirstValue("branchId"),out var b)?b:null;
    if(role=="Manager" && branch is null) return Results.Forbid();
    return resource.ToLowerInvariant() switch {
      "branches" when role=="BusinessHead" => Results.Ok(await db.Branches.AsNoTracking().ToListAsync()),
      "branches" when role=="Manager" => Results.Ok(await db.Branches.AsNoTracking().Where(x=>x.Id==branch).ToListAsync()),
      "students" => Results.Ok(await (role=="BusinessHead"?db.Students.AsQueryable():db.Students.Where(x=>x.BranchId==branch)).AsNoTracking().ToListAsync()),
      "trainers" => Results.Ok(await (role=="BusinessHead"?db.Trainers.AsQueryable():db.Trainers.Where(x=>x.BranchId==branch)).AsNoTracking().ToListAsync()),
      "courses" => Results.Ok(await (role=="BusinessHead"?db.Courses.AsQueryable():db.Courses.Where(x=>x.BranchId==branch||x.BranchId==null)).AsNoTracking().ToListAsync()),
      "admissions" => Results.Ok(await (role=="BusinessHead"?db.Admissions.AsQueryable():db.Admissions.Where(x=>x.BranchId==branch)).AsNoTracking().ToListAsync()),
      "fees" when role=="BusinessHead" => Results.Ok(await db.FeePayments.AsNoTracking().ToListAsync()),
      "fees" when role=="Manager" => Results.Ok(await db.FeePayments.AsNoTracking().Where(f=>db.Students.Any(s=>s.Id==f.StudentId && s.BranchId==branch)).ToListAsync()),
      "attendance" when role=="BusinessHead" => Results.Ok(await db.Attendance.AsNoTracking().ToListAsync()),
      "attendance" when role=="Manager" => Results.Ok(await db.Attendance.AsNoTracking().Where(a=>db.Students.Any(s=>s.Id==a.StudentId && s.BranchId==branch)).ToListAsync()),
      "assignments" => Results.Ok(await db.Assignments.AsNoTracking().Where(a=>role=="BusinessHead"||db.Courses.Any(c=>c.Id==a.CourseId && (c.BranchId==branch||c.BranchId==null))).ToListAsync()),
      "exams" => Results.Ok(await db.Exams.AsNoTracking().Where(e=>role=="BusinessHead"||db.Courses.Any(c=>c.Id==e.CourseId && (c.BranchId==branch||c.BranchId==null))).ToListAsync()),
      _ => Results.NotFound(new { message="Unknown resource" })
    };
}).RequireAuthorization();
app.MapPost("/api/admissions/enquiry", async (Admission input, AppDbContext db) => { input.Id=0; input.Status="Pending"; input.CreatedAt=DateTime.UtcNow; db.Admissions.Add(input); await db.SaveChangesAsync(); return Results.Created($"/api/admissions/{input.Id}",input); });
app.MapPost("/api/{resource}", async (string resource, JsonElement payload, AppDbContext db, ClaimsPrincipal u) => {
    var role=u.FindFirstValue(ClaimTypes.Role); int? branch=int.TryParse(u.FindFirstValue("branchId"),out var b)?b:null;
    if(role=="Manager" && branch is null) return Results.Forbid();
    switch(resource.ToLowerInvariant()) {
      case "branches": if(role!="BusinessHead") return Results.Forbid(); var br=payload.Deserialize<Branch>(); if(br is null)return Results.BadRequest(); db.Branches.Add(br); await db.SaveChangesAsync(); return Results.Created($"/api/branches/{br.Id}",br);
      case "students": var st=payload.Deserialize<Student>(); if(st is null)return Results.BadRequest(); if(role=="Manager") st.BranchId=branch; else if(st.BranchId is null) st.BranchId=branch; db.Students.Add(st); await db.SaveChangesAsync(); return Results.Created($"/api/students/{st.Id}",st);
      case "trainers": var tr=payload.Deserialize<Trainer>(); if(tr is null)return Results.BadRequest(); if(role=="Manager") tr.BranchId=branch; else if(tr.BranchId is null) tr.BranchId=branch; db.Trainers.Add(tr); await db.SaveChangesAsync(); return Results.Created($"/api/trainers/{tr.Id}",tr);
      case "courses": var co=payload.Deserialize<Course>(); if(co is null)return Results.BadRequest(); if(role=="Manager") co.BranchId=branch; else if(co.BranchId is null) co.BranchId=branch; db.Courses.Add(co); await db.SaveChangesAsync(); return Results.Created($"/api/courses/{co.Id}",co);
      case "fees": var fee=payload.Deserialize<FeePayment>(); if(fee is null)return Results.BadRequest(); if(role=="Manager" && !await db.Students.AnyAsync(s=>s.Id==fee.StudentId && s.BranchId==branch)) return Results.Forbid(); if(role!="BusinessHead" && role!="Manager") return Results.Forbid(); db.FeePayments.Add(fee); await db.SaveChangesAsync(); return Results.Created($"/api/fees/{fee.Id}",fee);
      case "attendance": var at=payload.Deserialize<AttendanceRecord>(); if(at is null)return Results.BadRequest(); if(role=="Manager" && !await db.Students.AnyAsync(s=>s.Id==at.StudentId && s.BranchId==branch)) return Results.Forbid(); if(role!="BusinessHead" && role!="Manager" && role!="Trainer") return Results.Forbid(); db.Attendance.Add(at); await db.SaveChangesAsync(); return Results.Created($"/api/attendance/{at.Id}",at);
      default:return Results.NotFound(new { message="Unknown resource" });
    }
}).RequireAuthorization();
app.Run();
static string GenerateTemporaryPassword() {
    const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
    var bytes = RandomNumberGenerator.GetBytes(20);
    return new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
}
record LoginRequest(string Email,string Password);
record SeedAdminRequest(string FullName,string Email,string Phone,string Password,string SetupKey);
record DevResetRequest(string Email,string Password,string SetupKey);
record CreateUserRequest(string FullName,string Email,string Role,int? BranchId);
record UpdateUserRequest(string FullName,string Email,string Role,int? BranchId);
record StatusRequest(bool IsActive);
record ChangePasswordRequest(string CurrentPassword,string NewPassword);
