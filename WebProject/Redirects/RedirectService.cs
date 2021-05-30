using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using EPiServer;
using EPiServer.Cms.Shell;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using EPiServer.Web.Routing;

namespace WebProject.Redirects
{
    public class RedirectService
    {
        private readonly IUrlResolver _urlResolver;
        private readonly IContentRepository _contentRepository;
        private readonly ISiteDefinitionRepository _siteDefinitionRepository;
        private readonly IPublishedStateAssessor _publishedStateAssessor;

        public RedirectService(IUrlResolver urlResolver, IContentRepository contentRepository, ISiteDefinitionRepository siteDefinitionRepository, IPublishedStateAssessor publishedStateAssessor)
        {
            _urlResolver = urlResolver;
            _contentRepository = contentRepository;
            _siteDefinitionRepository = siteDefinitionRepository;
            _publishedStateAssessor = publishedStateAssessor;
            RedirectRuleStorage.Init();
        }

        //You should use DI and remove this ServiceLocator constructor.
        public RedirectService()
        {
            _urlResolver = ServiceLocator.Current.GetInstance<IUrlResolver>();
            _contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();
            _siteDefinitionRepository = ServiceLocator.Current.GetInstance<ISiteDefinitionRepository>();
            _publishedStateAssessor = ServiceLocator.Current.GetInstance<IPublishedStateAssessor>();
            RedirectRuleStorage.Init();
        }

        private string GetModifiedBy(HttpContextBase httpContext) => httpContext.User.Identity.Name;

        public RedirectRule GetRedirect(int id)
        {
            if (!RedirectRuleStorage.IsUpToDate) return null;
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                return context.RedirectRules.FirstOrDefault(x => x.Id == id);
            }
        }

        public RedirectRule[] List()
        {
            if (!RedirectRuleStorage.IsUpToDate) return new RedirectRule[] { };

            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                return context.RedirectRules
                                .OrderBy(x => x.SortOrder)
                                .ThenBy(x => x.Host)
                                .ThenBy(x => x.FromUrl)
                                .ToArray();
            }
        }

        public RedirectRule[] GetListByHost(string host = "*")
        {
            if (!RedirectRuleStorage.IsUpToDate) return new RedirectRule[] { };

            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                return context.RedirectRules
                                .Where(x => !x.Deleted)
                                .Where(x => host == "*" || x.Host == null || x.Host == "*" || x.Host.Equals(host, StringComparison.InvariantCultureIgnoreCase))
                                .OrderBy(x => x.SortOrder)
                                .ThenBy(x => x.Host)
                                .ThenBy(x => x.FromUrl)
                                .ToArray();
            }
        }

        public RedirectRule[] GetLatestChanges(string host = "*", int maxItems = 20)
        {
            if (!RedirectRuleStorage.IsUpToDate) return new RedirectRule[] { };

            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                return context.RedirectRules
                                .Where(x => host == "*" || x.Host == null || x.Host == "*" || x.Host.Equals(host, StringComparison.InvariantCultureIgnoreCase))
                                .Where(x => x.Versions != null && x.Modified != null)
                                .OrderByDescending(x => x.Modified)
                                .Take(maxItems)
                                .ToArray();
            }
        }

        public List<string> GetExistingRulesHostNames()
        {
            if (!RedirectRuleStorage.IsUpToDate) return new List<string>();

            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                return context.RedirectRules.Select(x => x.Host).Distinct().ToList();
            }
        }

        public int AddRedirect(int? sortOrder, string host, string fromUrl, bool? wildcard, string toUrl, bool? relativeToFromUrl, int? toContentId, string toContentLang, string comment, HttpContextBase httpContext)
        {
            var r = GetRedirectRuleFromRequestParams(id: null, sortOrder, host, fromUrl, wildcard, toUrl, relativeToFromUrl, toContentId, toContentLang, comment, httpContext);
            return CreateOrUpdateRedirect(r);
        }

        public int ModifyRedirect(int id, int? sortOrder, string host, string fromUrl, bool? wildcard, string toUrl, bool? relativeToFromUrl, int? toContentId, string toContentLang, string comment, HttpContextBase httpContext)
        {
            var r = GetRedirectRuleFromRequestParams(id, sortOrder, host, fromUrl, wildcard, toUrl, relativeToFromUrl, toContentId, toContentLang, comment, httpContext);
            return CreateOrUpdateRedirect(r);
        }

        public RedirectRule SetBusinessRules(RedirectRule rule)
        {
            if (string.IsNullOrEmpty(rule.FromUrl))
                return null;

            //request end slash will be trimmed out. So, basic rules should not have it either
            if (!rule.Wildcard && rule.FromUrl.Length > 2 && rule.FromUrl.EndsWith("/"))
            {
                rule.Comment += "<br/>End slash (/) is trimmed out automatically.";
                rule.FromUrl = rule.FromUrl.TrimEnd('/');
            }

            //make from relative
            if (rule.IsFromUrlAbsolute)
            {
                rule.Comment += $"<br/>The Host from the From Url ({rule.FromUrl}) is segregated from the path.";
                rule.Host = rule.FromUri.Authority;
                rule.FromUrl = rule.FromUri.AbsolutePath.TrimEnd('/');
            }

            //make to relative if same hosts
            if (!rule.IsHostWildcard && rule.IsToUrlAbsolute && rule.ToUri.Host.Equals(rule.Host, StringComparison.InvariantCultureIgnoreCase))
            {
                rule.Comment += $"<br/>To URL has been converted to relative ({rule.ToUrl}).";
                rule.ToUrl = rule.ToUri.AbsolutePath.TrimEnd('/');
            }

            //convert rule as content id rule if possible
            if (!rule.Wildcard && !rule.IsHostWildcard && !string.IsNullOrEmpty(rule.ToUrl))
            {
                var content = rule.ToContent;
                if (content != null)
                {
                    rule.Comment += "<br/>Converted to acontent id rule (alias Episerver internal link)! To URL was: " + rule.ToUrl;
                    rule.ToContentId = content.ContentLink.ID;
                    rule.ToContentLang = content.LanguageBranch();
                    rule.ToUrl = null;
                }
            }

            //store backup version for restore and compare:
            rule.Versions += Environment.NewLine + SerializeToCsv(rule);

            return rule;
        }

        public List<string> ValidateRule(RedirectRule rule)
        {
            var result = new List<string>();
            //check from url doesn't exist
            if (!rule.Wildcard)
            {
                var content = rule.FromContent;
                if (content != null && _publishedStateAssessor.IsPublished(content))
                {
                    result.Add($"Content exists in From Url ({rule.FromUrl}). Rule will never be effective. Remove the existing content (id: {content.ContentLink?.ID}, page name: {content.Name}).");
                }
            }
            //check to url exits
            if (!string.IsNullOrEmpty(rule.ToUrl) && !rule.RelativeToFromUrl && !rule.ToUri.IsAbsoluteUri)
            {
                var content = rule.ToContent;
                if (content == null)
                {
                    result.Add($"There isn't any page in Episerver which will match the To Url ({rule.ToUrl}). Please, check that the page exists!");
                }
            }
            return result;
        }

        public int CreateOrUpdateRedirect(RedirectRule rule)
        {
            rule = SetBusinessRules(rule);
            if (rule == null) return 0;
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                if (rule.Id > 0)
                    context.Entry(rule).State = EntityState.Modified;
                else
                    context.RedirectRules.Add(rule);
                context.SaveChanges();
                return rule.Id;
            }
        }

        private RedirectRule GetRedirectRuleFromRequestParams(int? id, int? sortOrder, string host, string fromUrl, bool? wildcard, string toUrl, bool? relativeToFromUrl, int? toContentId, string toContentLang, string comment, HttpContextBase httpContext)
        {
            return GetRedirectRuleFromRequestParams(id, sortOrder, host, fromUrl, wildcard, toUrl, relativeToFromUrl, toContentId, toContentLang, comment, GetModifiedBy(httpContext));
        }

        private RedirectRule GetRedirectRuleFromRequestParams(int? id, int? sortOrder, string host, string fromUrl, bool? wildcard, string toUrl, bool? relativeToFromUrl, int? toContentId, string toContentLang, string comment, string modifiedBy)
        {
            var r = id.HasValue ? GetRedirect(id.Value) : new RedirectRule();
            //cleaning request params
            if (string.IsNullOrEmpty(toUrl) && toContentId.GetValueOrDefault(0) <= 0)
                return r;
            if (string.IsNullOrEmpty(toContentLang))
                toContentLang = null;
            if (string.IsNullOrEmpty(fromUrl))
                fromUrl = "/";
            if (toContentId == null && string.IsNullOrEmpty(toUrl))
                toUrl = "/";

            r.SortOrder = sortOrder.GetValueOrDefault(0);
            r.Host = host;
            r.FromUrl = fromUrl;
            r.Wildcard = wildcard.GetValueOrDefault(false);
            r.ToUrl = toUrl;
            r.RelativeToFromUrl = relativeToFromUrl.GetValueOrDefault(false);
            r.ToContentId = toContentId.GetValueOrDefault(0);
            r.ToContentLang = toContentLang;
            r.Modified = DateTime.UtcNow;
            r.ModifiedBy = modifiedBy;
            r.Comment = MakeCsvSafe(comment);
            return r;
        }
        string MakeCsvSafe(string comment)
        {
            return comment?.Replace("\r\n", "<br/>")
                            .Replace("\n", "<br/>")
                            .Replace("\r", "<br/>")
                            .Replace(";", ":");
        }

        public int DeleteRedirect(int id)
        {
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                var r = context.RedirectRules.First(x => x.Id == id);
                r.Deleted = true;
                context.Entry(r).State = EntityState.Modified;
                return context.SaveChanges();
            }
        }

        public string GetPrimaryRedirectUrlOrDefault(string host, string relativeUrl)
        {
            if (!RedirectRuleStorage.IsUpToDate) return null;
            if (string.IsNullOrEmpty(relativeUrl))
                return null;
            if (relativeUrl.Length > 1 && relativeUrl.Last() == '/')
                relativeUrl = relativeUrl.Remove(relativeUrl.Length - 1);
            relativeUrl = relativeUrl.ToLower();
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                var exactMatch = context.RedirectRules
                                .Where(x => !x.Deleted)
                                .Where(x => x.Host == null || x.Host == "*" || x.Host.Equals(host, StringComparison.InvariantCultureIgnoreCase))
                                .Where(x => x.FromUrl.Equals(relativeUrl, StringComparison.InvariantCultureIgnoreCase))
                                .OrderBy(x => x.SortOrder)
                                .ThenBy(x => x.FromUrl)
                                .FirstOrDefault();

                var wildcards = context.RedirectRules
                                .Where(x => !x.Deleted)
                                .Where(x => x.Host == null || x.Host == "*" || x.Host.Equals(host, StringComparison.InvariantCultureIgnoreCase))
                                .Where(x => x.Wildcard)
                                .OrderBy(x => x.SortOrder)
                                .ThenBy(x => x.FromUrl);
                var match = wildcards.FirstOrDefault(x => relativeUrl.StartsWith(x.FromUrl)
                                                        || relativeUrl.Equals(x.FromUrl, StringComparison.InvariantCultureIgnoreCase));

                RedirectRule theMatch = (exactMatch != null && match != null)
                                    ? exactMatch.SortOrder < match.SortOrder ? exactMatch : match
                                    : exactMatch ?? match;
                if (theMatch == null) return null;

                return ResolveToUrl(relativeUrl, theMatch);
            }
        }

        public string ResolveToUrl(string relativeUrl, RedirectRule theMatch)
        {
            if (theMatch.ToContentId > 0)
            {
                return _urlResolver.GetUrl(new ContentReference(theMatch.ToContentId), theMatch.ToContentLang);
            }
            return theMatch.Wildcard && theMatch.RelativeToFromUrl ? relativeUrl.Replace(theMatch.FromUrl, theMatch.ToUrl) : theMatch.ToUrl;
        }

        public string ResolveToUrl(RedirectRule theMatch, bool relativeToHost = true)
        {
            string toUrl =  (theMatch.ToContentId > 0) 
                            ? _urlResolver.GetUrl(new ContentReference(theMatch.ToContentId), theMatch.ToContentLang)
                            : theMatch.ToUrl;
            if (relativeToHost)
            {
                return toUrl.Replace("https://" + theMatch.Host, "")
                            .Replace("http://" + theMatch.Host, "");
            }
            return toUrl;

        }

        public string[] GetGlobalLanguageOptions()
        {
            var enabledLanguages = ServiceLocator.Current.GetInstance<ILanguageBranchRepository>().ListEnabled();
            return enabledLanguages.Select(l => l.Culture.Name).ToArray();
        }

        public int ImportCsv(HttpContextBase httpContext, string fieldName = "importFile")
        {
            HttpPostedFileBase file = httpContext.Request.Files.Get(fieldName);
            if (file == null || file.ContentLength <= 0)
                return 0;
            var csvLines = GetCsvLines(file);
            bool skipHeader = csvLines.FirstOrDefault()?.Contains("FromUrl") ?? false;
            List<RedirectRule> values = csvLines.Skip(skipHeader ? 1 : 0).Select(DeserializeCsv).Where(r => r != null).ToList();
            var allRules = List();
            var newRuleValues = values.Where(r => allRules.All(ar => !r.CompareEffectiveValues(ar))).ToList();
            var resultCount = newRuleValues.Select(nr =>
            {
                bool isExistingRule = nr.Id > 0 && allRules.Any(r => r.Id == nr.Id && r.Host.Equals(nr.Host, StringComparison.InvariantCultureIgnoreCase));
                if (!isExistingRule)
                    nr.Id = default(int);
                nr.Modified = DateTime.UtcNow;
                nr.ModifiedBy = httpContext.User.Identity.Name;
                int newId = CreateOrUpdateRedirect(nr);
                return newId > 0 ? 1 : 0;
            })
            .Sum();
            return resultCount;
        }

        public string ExportCsv(string host = "*", bool addHeaders = false)
        {
            var csv = new StringBuilder();
            var rules = GetListByHost(host);
            if (addHeaders)
                csv.AppendLine(CsvHeaders());
            foreach (RedirectRule rule in rules)
            {
                csv.AppendLine(SerializeToCsv(rule));
            }
            return csv.ToString();
        }

        public static string CsvHeaders()
        {
            return "\"Id\";" +
                   "\"Host\";" +
                   "\"FromUrl\";" +
                   "\"ToUrl\";" +
                   "\"Wildcard\";" +
                   "\"RelativeToFromUrl\";" +
                   "\"ToContentId\";" +
                   "\"ToContentLang\";" +
                   "\"SortOrder\";" +
                   "\"Modified\";" +
                   "\"ModifiedBy\";" +
                   "\"Deleted\";" +
                   "\"Comment\"";
        }

        public static string SerializeToCsv(RedirectRule rule)
        {
            return $"\"{rule.Id}\";" +
                   $"\"{rule.Host}\";" +
                   $"\"{rule.FromUrl}\";" +
                   $"\"{rule.ToUrl}\";" +
                   $"\"{rule.Wildcard}\";" +
                   $"\"{rule.RelativeToFromUrl}\";" +
                   $"\"{rule.ToContentId}\";" +
                   $"\"{rule.ToContentLang}\";" +
                   $"\"{rule.SortOrder}\";" +
                   $"\"{rule.Modified?.ToString("s", System.Globalization.CultureInfo.InvariantCulture)}\";" +
                   $"\"{rule.ModifiedBy}\";" +
                   $"\"{rule.Deleted}\";" +
                   $"\"{rule.Comment}\"";
        }


        private static List<string> GetCsvLines(HttpPostedFileBase file)
        {
            List<string> csvLines = new List<string>();
            using (System.IO.StreamReader reader = new System.IO.StreamReader(file.InputStream))
            {
                while (!reader.EndOfStream)
                {
                    csvLines.Add(reader.ReadLine());
                }
            }
            return csvLines;
        }

        private static RedirectRule DeserializeCsv(string csvLine)
        {
            string trimmed = csvLine.Trim(new[] { '"' });
            string separator = trimmed.Contains("\";\"") ? "\";\"" : ";"; //Excel doesn't normally use quotation marks (")
            string[] values = trimmed.Split(new string[] { separator }, StringSplitOptions.None);

            if (values.Length <= 3) //there are required fields
                return null;
            RedirectRule result = new RedirectRule();
            result.Id = Convert.ToUInt16(EmptyIs(values.ElementAtOrDefault(0), "0"));
            result.Host = Convert.ToString(EmptyIs(values.ElementAtOrDefault(1), null));
            result.FromUrl = Convert.ToString(EmptyIs(values.ElementAtOrDefault(2), null));
            result.ToUrl = Convert.ToString(EmptyIs(values.ElementAtOrDefault(3), null));
            result.Wildcard = Convert.ToBoolean(EmptyIs(values.ElementAtOrDefault(4), "False"));
            result.RelativeToFromUrl = Convert.ToBoolean(EmptyIs(values.ElementAtOrDefault(5), null));
            result.ToContentId = Convert.ToInt16(EmptyIs(values.ElementAtOrDefault(6), "0"));
            result.ToContentLang = Convert.ToString(EmptyIs(values.ElementAtOrDefault(7), null));
            result.SortOrder = Convert.ToUInt16(EmptyIs(values.ElementAtOrDefault(8), "100"));
            result.Modified = Convert.ToDateTime(EmptyIs(values.ElementAtOrDefault(9), null));
            result.ModifiedBy = Convert.ToString(EmptyIs(values.ElementAtOrDefault(10), DateTime.Now.ToString("s")));
            result.Deleted = Convert.ToBoolean(EmptyIs(values.ElementAtOrDefault(11), "False"));
            result.Comment = Convert.ToString(EmptyIs(values.ElementAtOrDefault(12), null));
            return result;
        }

        public static string EmptyIs(string value, string defaultValue)
        {
            return !string.IsNullOrEmpty(value) ? value : defaultValue;
        }

        public List<(string Title, List<(string Host, bool? UseSecureConnection)> Hosts)> GetHostOptions()
        {
            var siteDefinitionHosts = _siteDefinitionRepository.List()
                .SelectMany(s => s.Hosts.Select(h => (Host: h.Name, UseSecureConnection: h.UseSecureConnection)))
                .OrderBy(h => h.Host)
                .ToList();
            List<string> existingRulesHostNames = GetExistingRulesHostNames();
            var existingHosts = existingRulesHostNames.OrderBy(x => x)
                .Select(h => (Host: h, UseSecureConnection: siteDefinitionHosts.FirstOrDefault(d => d.Host == h).UseSecureConnection)).ToList();
            var otherHosts = siteDefinitionHosts.Where(sh => !existingRulesHostNames.Contains(sh.Host)).ToList();
            return new[]
            {
                (Title: "Used Hosts", Hosts: existingHosts),
                (Title: "Other Host options", Hosts: otherHosts)
            }
            .ToList();
        }


        public static (bool IsInit, IContent Content) GetFromContent(RedirectRule rule)
        {
            if (string.IsNullOrEmpty(rule.FromUrl))
                return (IsInit: true, Content: null);
            string absoluteOrRelative = rule.IsFromUrlAbsolute || rule.IsHostWildcard ? rule.FromUrl : "https://" + rule.Host + rule.FromUrl;
            var content = GetContentOrDefault(absoluteOrRelative);
            return (IsInit: true, Content: content);
        }

        public static (bool IsInit, IContent Content) GetToContent(RedirectRule rule)
        {
            if (rule.ToContentId > 0)
            {
                var page = ServiceLocator.Current.GetInstance<IContentRepository>().TryGet(new ContentReference(rule.ToContentId), !string.IsNullOrEmpty(rule.ToContentLang) ? new CultureInfo(rule.ToContentLang) : null, out IContent result) ? result : null;
                return (IsInit: true, Content: page);
            }
            if (string.IsNullOrEmpty(rule.ToUrl))
                return (IsInit: true, Content: null);

            string absoluteOrRelative = AbsoluteOrRelative(rule);
            var content = GetContentOrDefault(absoluteOrRelative);
            return (IsInit: true, Content: content);
        }

        private static string AbsoluteOrRelative(RedirectRule rule)
        {
            //TODO: now we only validate https host names.
            return rule.IsToUrlAbsolute || rule.IsHostWildcard ? rule.ToUrl : "https://" + rule.Host + rule.ToUrl;
        }

        private static IContent GetContentOrDefault(string absoluteOrRelative)
        {
            try { return ServiceLocator.Current.GetInstance<IUrlResolver>().Route(new UrlBuilder(absoluteOrRelative), ContextMode.Default); }
            catch (UriFormatException) { return null; }
        }
    }

    //Class is for avoiding use of any migration framework. Not pretty but it does the simple job.
    public static class RedirectRuleStorage
    {
        public const string RedirectTableName = "SOLITA_Redirect";
        public static bool IsUpToDate => TableExist && TableV2 && TableV3 && TableV4;
        public static bool TableExist { get; private set; }
        public static bool TableV2 { get; private set; }
        public static bool TableV3 { get; private set; }
        public static bool TableV4 { get; private set; }

        public static void Init()
        {
            if (!IsUpToDate)
            {
                TableV4 = TableV4Exists();
                TableV3 = TableV3Exists();
                TableV2 = TableV2Exists();
                TableExist = RedirectTableExists();
            }
        }

        public static bool CreateTable()
        {
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                context.Database.ExecuteSqlCommand(
                    @"CREATE TABLE [dbo].[" + RedirectTableName + @"](
                    [Id][int] IDENTITY(1, 1) NOT NULL,
                    [SortOrder][int] NOT NULL,
                    [Host][nvarchar](max) NULL,
                    [FromUrl][nvarchar](max) NULL,
                    [ToUrl][nvarchar](max) NULL,
                    [ToContentId][int] NOT NULL,
                    [ToContentLang][nvarchar](10) NULL,
	                [Wildcard] [bit] NOT NULL DEFAULT ((0)),
                    [RelativeToFromUrl] [bit] NOT NULL DEFAULT ((0)),
                    [Modified] DATETIME NULL,
                    [ModifiedBy][nvarchar](max) NULL,
                    [Comment][nvarchar](max) NULL,
                    [Versions][nvarchar](max) NULL,
	                [Deleted] [bit] NOT NULL DEFAULT ((0)),
                 CONSTRAINT[PK_dbo." + RedirectTableName + @"] PRIMARY KEY CLUSTERED
                ( [Id] ASC)WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY]) ON[PRIMARY] TEXTIMAGE_ON[PRIMARY]
                ");
                return TableExist = TableV2 = TableV3 = TableV4 = true;
            }
        }

        public static void UpdateTableToLatest()
        {
            if (!TableExist) CreateTable();
            if (!TableV2) UpdateTableV2();
            if (!TableV3) UpdateTableV3();
            if (!TableV4) UpdateTableV4();
        }

        public static bool UpdateTableV2()
        {
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                context.Database.ExecuteSqlCommand(
                    @"ALTER TABLE [dbo].[" + RedirectTableName + @"] ADD [Host][nvarchar](max) NULL"
                );
                return TableV2 = true;
            }
        }

        public static bool UpdateTableV3()
        {
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                context.Database.ExecuteSqlCommand(
                    @"ALTER TABLE [dbo].[" + RedirectTableName + @"] ADD [RelativeToFromUrl] [bit] NOT NULL DEFAULT ((0))"
                );
                return TableV3 = true;
            }
        }

        public static bool UpdateTableV4()
        {
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                context.Database.ExecuteSqlCommand(
                    @"ALTER TABLE [dbo].[" + RedirectTableName + @"] ADD [Modified] DATETIME NULL"
                );
                context.Database.ExecuteSqlCommand(
                    @"ALTER TABLE [dbo].[" + RedirectTableName + @"] ADD [ModifiedBy][nvarchar](max)NULL"
                );
                context.Database.ExecuteSqlCommand(
                    @"ALTER TABLE [dbo].[" + RedirectTableName + @"] ADD [Comment][nvarchar](max)NULL"
                );
                context.Database.ExecuteSqlCommand(
                    @"ALTER TABLE [dbo].[" + RedirectTableName + @"] ADD [Versions][nvarchar](max)NULL"
                );
                context.Database.ExecuteSqlCommand(
                    @"ALTER TABLE [dbo].[" + RedirectTableName + @"] ADD [Deleted] [bit] NOT NULL DEFAULT ((0))"
                );
                return TableV4 = true;
            }
        }


        public static bool RedirectTableExists()
        {
            if (TableV2) return true;
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                var t = context.Database.SqlQuery<int?>(@"
                         SELECT 1 FROM sys.tables AS T
                         INNER JOIN sys.schemas AS S ON T.schema_id = S.schema_id
                         WHERE S.Name = 'dbo' AND T.Name = '" + RedirectTableName + @"'")
                         .SingleOrDefault() != null;
                return TableExist = t;
            }
        }
        public static bool TableV2Exists()
        {
            if (TableV3) return true;
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                var t = context.Database.SqlQuery<int?>(@"
                        select 1 from INFORMATION_SCHEMA.columns where
                            table_name = '" + RedirectTableName + @"'
                            and column_name = 'Host'")
                         .SingleOrDefault() != null;
                return TableV2 = t;
            }
        }

        public static bool TableV3Exists()
        {
            if (TableV4) return true;
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                var t = context.Database.SqlQuery<int?>(@"
                        select 1 from INFORMATION_SCHEMA.columns where
                            table_name = '" + RedirectTableName + @"'
                            and column_name = 'RelativeToFromUrl'")
                         .SingleOrDefault() != null;
                return TableV3 = t;
            }
        }

        public static bool TableV4Exists()
        {
            //if (TableV5) return true;
            using (var context = ServiceLocator.Current.GetInstance<RedirectDbContext>())
            {
                var t = context.Database.SqlQuery<int?>(@"
                        select 1 from INFORMATION_SCHEMA.columns where
                            table_name = '" + RedirectTableName + @"'
                            and column_name = 'Deleted'")
                         .SingleOrDefault() != null;
                return TableV4 = t;
            }
        }
    }
}
