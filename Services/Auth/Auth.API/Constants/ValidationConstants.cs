namespace Auth.API.Constants;

public static class ValidationConstants
{
    // Password
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const string PasswordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]";

    // Phone
    public const string VietnamesePhoneRegex = @"^(0[3|5|7|8|9])+([0-9]{8})$";

    // Name
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const string VietnameseNameRegex = @"^[a-zA-ZÀ-ỹ\s]+$";

    // Email
    public const int EmailMaxLength = 255;

    // Department
    public const int DepartmentNameMinLength = 2;
    public const int DepartmentNameMaxLength = 100;
    public const int DepartmentDescriptionMaxLength = 500;
    public const string DepartmentNameRegex = @"^[a-zA-ZÀ-ỹ0-9\s\-_]+$";

    // Role
    public const int RoleNameMinLength = 2;
    public const int RoleNameMaxLength = 50;
    public const int RoleDescriptionMaxLength = 300;
    public const string RoleNameRegex = @"^[a-zA-ZÀ-ỹ0-9\s\-_]+$";

    // Permission
    public const int PermissionNameMinLength = 2;
    public const int PermissionNameMaxLength = 100;
    public const int PermissionDescriptionMaxLength = 300;
    public const string PermissionNameRegex = @"^[a-zA-Z0-9\.\-_]+$";
}