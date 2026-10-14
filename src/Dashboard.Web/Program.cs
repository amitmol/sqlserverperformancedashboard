using Dashboard.Backend;
using Dashboard.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("This synthetic local application requires ASPNETCORE_ENVIRONMENT=Development.");
builder.WebHost.UseUrls("http://127.0.0.1:5080");
var rawConnection = Environment.GetEnvironmentVariable("DashboardDevelopmentConnection")
    ?? throw new InvalidOperationException("Set DashboardDevelopmentConnection using the local startup script.");
var sql = new SqlConnectionStringBuilder(rawConnection);
if (!string.Equals(sql.DataSource, @"(localdb)\SQLDashboardDev", StringComparison.OrdinalIgnoreCase) ||
    sql.InitialCatalog != "SQLDashboard_Development" || !sql.IntegratedSecurity)
    throw new InvalidOperationException("Only the dedicated SQLDashboardDev LocalDB database with Windows authentication is supported.");

builder.Services.AddControllersWithViews().AddJsonOptions(options =>
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddAuthentication("LocalDevelopment").AddScheme<AuthenticationSchemeOptions, LocalDevelopmentAuthentication>("LocalDevelopment", _ => { });
builder.Services.AddAuthorization(options => options.AddPolicy("Monitoring", policy =>
    policy.RequireAuthenticatedUser().RequireClaim("monitoring-instance", LocalDevelopmentAuthentication.InstanceId)));
builder.Services.AddScoped(_ => new SqlSnapshotRepository(() => new SqlConnection(rawConnection)));
builder.Services.AddScoped<DashboardReadService>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Host.Host != "127.0.0.1" && context.Request.Host.Host != "localhost")
    { context.Response.StatusCode = 400; return; }
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=SqlMonitoring}/{action=Index}");
app.Run();
