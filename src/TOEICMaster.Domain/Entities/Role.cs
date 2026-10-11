using System;
using System.Collections.Generic;
using System.Text;


namespace TOEICMaster.Domain.Entities;

public class Role
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; }
        = new List<UserRole>();
}

