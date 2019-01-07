using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Newtonsoft.Json;

namespace Helix.Crawler.Specifications
{
    class InternalResourceDefinition : TheoryDescription<Configurations, Resource, bool, Type>
    {
        public InternalResourceDefinition()
        {
            ShareHostNameWithParent();
            MatchConfiguredDomainName();
            IsStartUrlUsingDomainName();
            IsStartUrlUsingIpAddress();

            ThrowExceptionIfArgumentNull();
            ThrowExceptionIfArgumentIsNotValid();

            IsNotInternalResourceInAllOtherCases();
        }

        [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
        void IsNotInternalResourceInAllOtherCases()
        {
            var resource = new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.sanity.com/anything") };
            AddTheoryDescription(p2: resource, p3: false);
        }

        void IsStartUrlUsingDomainName()
        {
            var resource = new Resource { ParentUri = null, Uri = new Uri("http://www.helix.com") };
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);
        }

        void IsStartUrlUsingIpAddress()
        {
            var resource = new Resource { ParentUri = null, Uri = new Uri("http://www.helix.com") };
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.StartUrl), "http://192.168.1.2" },
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);
        }

        [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
        void MatchConfiguredDomainName()
        {
            var resource = new Resource { ParentUri = new Uri("http://192.168.1.2/parent"), Uri = new Uri("http://www.helix.com/child") };
            var configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.DomainName), "www.helix.com" }
            }));
            AddTheoryDescription(configurations, resource, true);

            resource = new Resource { ParentUri = new Uri("http://192.168.1.2"), Uri = new Uri("http://www.helix.com/anything") };
            configurations = new Configurations(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { nameof(Configurations.DomainName), "helix.com" }
            }));
            AddTheoryDescription(configurations, resource, false);
        }

        void ShareHostNameWithParent()
        {
            var resource = new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.helix.com/anything") };
            AddTheoryDescription(p2: resource, p3: true);
        }

        void ThrowExceptionIfArgumentIsNotValid()
        {
            var resource = new Resource { Uri = new Uri("http://www.helix.com"), ParentUri = null };
            AddTheoryDescription(p2: resource, p4: typeof(ArgumentException));
        }

        void ThrowExceptionIfArgumentNull()
        {
            var resource = new Resource { Uri = null };
            AddTheoryDescription(p2: resource, p4: typeof(ArgumentNullException));
            AddTheoryDescription(p2: null, p4: typeof(ArgumentNullException));
        }
    }
}