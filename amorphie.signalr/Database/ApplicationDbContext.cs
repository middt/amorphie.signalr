using amorphie.signalr.Models;
using Microsoft.EntityFrameworkCore;

namespace amorphie.signalr.Database;


public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Message> Messages { get; set; }
}