using Microsoft.Extensions.Options;
using PentiaCase.Web.Services.Api;
using PentiaCase.Web.Services.Caching;
using PentiaCase.Web.Services.Reports;
using PentiaCase.Web.Services.Salespeople;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IValidateOptions<WingineersApiOptions>, WingineersApiOptionsValidator>();
builder.Services.AddOptions<WingineersApiOptions>()
    .BindConfiguration(WingineersApiOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<WingineersApiClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<WingineersApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("ApiKey", options.ApiKey);
}).RedactLoggedHeaders(_ => true);

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton(services => new WingineersDataCache(
    () => services.GetRequiredService<WingineersApiClient>(),
    services.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<SalespeopleService>();
builder.Services.AddSingleton<MonthlyOrdersService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
