public class GetUserByDeparAndRole
{
    public Guid DepartmentId { get; set; }
    public Guid RoleId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
