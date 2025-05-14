using System;

namespace Auth.API.Utils;

public class OtpUtil
{
    public static string GenerateOtp()
    {
        Random random = new Random();
        return random.Next(100000, 999999).ToString();
    }
}