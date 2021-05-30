using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using EPiServer.Core;

namespace WebProject.Redirects
{
    [Table(RedirectRuleStorage.RedirectTableName)]
    public class RedirectRule
    {
        [Key]
        public int Id { get; set; }
        public string Host { get; set; }
        public int SortOrder { get; set; }
        public string FromUrl { get; set; }
        public string ToUrl { get; set; }
        public int ToContentId { get; set; }
        public string ToContentLang { get; set; }
        public bool Wildcard { get; set; }
        public bool RelativeToFromUrl { get; set; }
        public DateTime? Modified { get; set; }
        public string ModifiedBy { get; set; }
        public string Comment { get; set; }
        public bool Deleted { get; set; }
        public string Versions { get; set; }

        public List<string> VersionLines => (Versions ?? "").Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
        public int VersionCount => VersionLines.Count;

        public bool IsHostWildcard => string.IsNullOrEmpty(Host) || Host == "*";
        public Uri FromUri => new Uri(FromUrl, UriKind.RelativeOrAbsolute);
        public bool IsFromUrlAbsolute => FromUri.IsAbsoluteUri;
        public Uri ToUri => new Uri(ToUrl, UriKind.RelativeOrAbsolute);
        public bool IsToUrlAbsolute => ToUri.IsAbsoluteUri;

        private (bool IsInit, IContent Content) _fromContent;
        public IContent FromContent => (_fromContent.IsInit ? _fromContent : (_fromContent = RedirectService.GetFromContent(this))).Content;

        private (bool IsInit, IContent Content) _toContent;
        public IContent ToContent => (_toContent.IsInit ? _toContent : (_toContent = RedirectService.GetToContent(this))).Content;



        private string _effectiveValueshash = null;
        public string EffectiveValuesHash => _effectiveValueshash ?? (_effectiveValueshash = CreateHash(this));

        /// <summary>
        /// Effective values are not Id, Comments, Versions, Modified, ModifiedBy
        /// </summary>
        public bool CompareEffectiveValues(RedirectRule rule)
        {
            return this.EffectiveValuesHash == rule.EffectiveValuesHash;
        }

        public static string CreateHash(RedirectRule r) => $"{r.Host}{r.FromUrl}{r.Wildcard}{r.ToUrl}{r.ToContentId}{r.ToContentLang}{r.RelativeToFromUrl}{r.Deleted}";
    }
}
