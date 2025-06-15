using System.ComponentModel.DataAnnotations;

namespace Auth.Domain.Models;

public class ActiveKey
{
    [Key]
    public Guid Id { get; set; }
    public string ActivationCode  { get; set; }
    public string RoleName { get; set; }
}