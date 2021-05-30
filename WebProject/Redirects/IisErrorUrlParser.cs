using System;
using System.Text.RegularExpressions;

namespace WebProject.Redirects
{
    public static class IisErrorUrlParser
    {
        public static string GetOriginalRelativePath(Uri iisHttpErrorUri, int statusCode)
        {
            //example: https://www.example-domain.com/error/http404?404;https://example-domain:80/some/path?query=string
            Regex urlRegex = new Regex($"\\?404;(\\w+):\\/\\/(.*):(\\d?)(\\d?)(\\d?)(\\d?)(\\d?)\\/(?<relativePath>.*)", RegexOptions.Compiled);
            string relativePath = urlRegex.Match(iisHttpErrorUri.Query)?.Groups["relativePath"]?.Value;
            return relativePath != null ? "/" + relativePath : null;
        }
    }
}