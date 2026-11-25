
using Microsoft.AspNetCore.Identity;

namespace QuanLyKho2221.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();

            // Tạo Role
            string[] roles = { "Admin", "Employee" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Tạo tài khoản Admin
            var adminEmail = "admin@quanlykho.com";
            var admin = await userManager.FindByNameAsync("admin");

            if (admin == null)
            {
                admin = new IdentityUser
                {
                    UserName = "admin",
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(admin, "Admin@123");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
                else
                {
                    throw new Exception(string.Join("; ",
                        result.Errors.Select(e => e.Description)));
                }
            }

            // Tạo tài khoản Employee
            var employee = await userManager.FindByNameAsync("employee");

            if (employee == null)
            {
                employee = new IdentityUser
                {
                    UserName = "employee",
                    Email = "employee@quanlykho.com",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(employee, "Employee@123");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(employee, "Employee");
                }
                else
                {
                    throw new Exception(string.Join("; ",
                        result.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
