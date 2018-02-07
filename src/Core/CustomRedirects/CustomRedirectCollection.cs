using System;
using System.Collections;
using System.Web;
using BVNetwork.NotFound.Core.Configuration;
using BVNetwork.NotFound.Core.Data;

namespace BVNetwork.NotFound.Core.CustomRedirects
{
    /// <summary>
    /// A collection of custom urls
    /// </summary>
    public class CustomRedirectCollection: CollectionBase
    {
        private readonly IConfiguration _configuration;

        public CustomRedirectCollection()
            : this(Configuration.Configuration.Instance)
        {
        }

        public CustomRedirectCollection(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Hashtable for quick lookup of old urls
        /// </summary>
        private readonly Hashtable _quickLookupTable = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

        public CustomRedirect Find(Uri urlNotFound)
        {
            // Handle absolute addresses first
            var url = urlNotFound.AbsoluteUri;
            var foundRedirect = FindInternal(url);

            // Common case
            if (foundRedirect == null)
            {
                url = urlNotFound.PathAndQuery;
                foundRedirect = FindInternal(url);
            }

            // Handle legacy databases with encoded values
            if (foundRedirect == null)
            {
                url = HttpUtility.HtmlEncode(url);
                foundRedirect = FindInternal(url);
            }

            if (foundRedirect == null)
            {
                url = urlNotFound.AbsoluteUri;
                foundRedirect = FindInProviders(url);
            }

            return foundRedirect;
        }

        public int Add(CustomRedirect customRedirect)
        {
            // Add to quick look up table too
            _quickLookupTable.Add(customRedirect.OldUrl, customRedirect);
            return List.Add(customRedirect);
        }

        private CustomRedirect FindInternal(string url)
        {
            var foundRedirect = _quickLookupTable[url];
            if (foundRedirect != null)
            {
                return foundRedirect as CustomRedirect;
            }

            // No exact match could be done, so we'll check if the 404 url
            // starts with one of the urls we're matching against. This
            // will be kind of a wild card match (even though we only check
            // for the start of the url
            // Example: http://www.mysite.com/news/mynews.html is not found
            // We have defined an "<old>/news</old>" entry in the config
            // file. We will get a match on the /news part of /news/myne...
            // Depending on the skip wild card append setting, we will either
            // redirect using the <new> url as is, or we'll append the 404
            // url to the <new> url.
            var enumerator = _quickLookupTable.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var key = enumerator.Key?.ToString();
                // See if this "old" url (the one that cannot be found) starts with one
                if (key != null && url.StartsWith(key, StringComparison.InvariantCultureIgnoreCase))
                {
                    foundRedirect = _quickLookupTable[enumerator.Key];
                    var cr = foundRedirect as CustomRedirect;
                    if (cr != null && cr.State == (int) DataStoreHandler.State.Ignored)
                    {
                        return null;
                    }
                    if (cr != null && cr.WildCardSkipAppend)
                    {
                        // We'll redirect without appending the 404 url
                        return cr;
                    }

                    // We need to append the 404 to the end of the
                    // new one. Make a copy of the redir object as we
                    // are changing it.
                    var redirCopy = new CustomRedirect(cr);
                    redirCopy.NewUrl = redirCopy.NewUrl + url.Substring(enumerator.Key.ToString().Length);
                    return redirCopy;
                }
            }
            return null;
        }

        private CustomRedirect FindInProviders(string oldUrl)
        {
            // If no exact or wildcard match is found, try to parse the url through the custom providers
            foreach (var provider in _configuration.Providers)
            {
                var newUrl = provider.RewriteUrl(oldUrl);
                if (newUrl != null) return new CustomRedirect(oldUrl, newUrl);
            }
            return null;
        }

        public bool Contains(string oldUrl)
        {
            return _quickLookupTable.ContainsKey(oldUrl);
        }

        public int IndexOf(CustomRedirect customRedirect)
        {
            for(var i = 0; i < List.Count; i++)
            {
                if (this[i] == customRedirect) return i; // Found
            }
            return -1;
        }

        public CustomRedirect this[int index]
        {
            get => (CustomRedirect) List[index];
            set => List[index] = value;
        }

        public void Insert(int index, CustomRedirect customRedirect)
        {
            _quickLookupTable.Add(customRedirect, customRedirect);
            List.Insert(index, customRedirect);
        }

        public void Remove(CustomRedirect customRedirect)
        {
            _quickLookupTable.Remove(customRedirect);
            List.Remove(customRedirect);
        }
    }
}
