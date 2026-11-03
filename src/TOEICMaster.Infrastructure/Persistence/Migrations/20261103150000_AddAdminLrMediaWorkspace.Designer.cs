using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TOEICMaster.Infrastructure.Persistence;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ToeicDbContext))]
[Migration("20261103150000_AddAdminLrMediaWorkspace")]
partial class AddAdminLrMediaWorkspace;
