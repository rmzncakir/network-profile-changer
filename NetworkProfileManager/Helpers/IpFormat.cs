using System.Net;
using System.Numerics;

namespace NetworkProfileManager.Helpers
{
    /// <summary>Compact display forms for IPv4 settings.</summary>
    public static class IpFormat
    {
        /// <summary>
        /// Prefix length of a dotted-quad subnet mask ("255.255.255.0" → 24), or
        /// null when the text is not a contiguous IPv4 mask.
        /// </summary>
        public static int? PrefixLength(string? mask)
        {
            if (string.IsNullOrWhiteSpace(mask)
                || !IPAddress.TryParse(mask.Trim(), out var parsed)
                || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return null;

            var b = parsed.GetAddressBytes();
            uint m = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];

            // A valid mask is ones followed by zeros: its inverse is 0…01…1,
            // and adding one to that clears every bit it had.
            uint inverse = ~m;
            if ((inverse & (inverse + 1)) != 0) return null;

            return BitOperations.PopCount(m);
        }

        /// <summary>
        /// "192.168.0.10/24" when the mask is a valid prefix, otherwise
        /// "192.168.0.10 / 255.255.0.255" so an odd mask is still visible.
        /// </summary>
        public static string WithPrefix(string ip, string? mask)
        {
            if (string.IsNullOrWhiteSpace(mask)) return ip;
            return PrefixLength(mask) is int prefix
                ? $"{ip}/{prefix}"
                : $"{ip} / {mask}";
        }
    }
}
