using Ztatic;
using Ztatic.Sample.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents();

builder.Services.AddZtatic(opt =>
{
    opt.ExplicitUrlsToFetch.Add("/explicit-fetch");
    opt.PageOutputStyle = OutputStyle.AppendHtmlExtension;
    opt.ContentToCopyToOutput.Add(new ContentToCopy("ExtraContentToCopy", "ExtraContentToCopy"));
    opt.SiteUrl = "https://myapp.com";
    
    opt.ConfigureContentPipeline(pipeline =>
    {
        pipeline.CreateFiles();
    });
    
    opt.GenerateSitemap();
});

// builder.WebHost.UseStaticWebAssets();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.UseZtaticGenerator(!app.Environment.IsDevelopment());

app.Run();