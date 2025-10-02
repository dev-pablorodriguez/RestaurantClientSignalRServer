namespace RestaurantClientSignalRServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var services = builder.Services;
            services.AddRazorPages();
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
            });
            services.AddCors(options =>
            {
                options.AddPolicy("default", policy =>
                {
                    policy
                         .WithOrigins("https://icy-stone-02ca7881e.2.azurestaticapps.net")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseCors("default");

            app.UseRouting();

            app.UseAuthorization();

            app.MapRazorPages();

            app.MapHub<OrderHub>("/orderHub");

            app.Run();
        }
    }
}
