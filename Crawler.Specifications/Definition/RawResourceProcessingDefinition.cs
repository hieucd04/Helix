﻿using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;

namespace Helix.Crawler.Specifications
{
    class RawResourceProcessingDefinition : TheoryDescription<RawResource, Resource, bool, Type>
    {
        public RawResourceProcessingDefinition()
        {
            CreateResourceFromRawResource();
            ConvertRelativeUrlToAbsoluteUrl();
            ThrowExceptionIfArgumentNull();
            ThrowExceptionIfArgumentIsNotValid();
        }

        void ConvertRelativeUrlToAbsoluteUrl()
        {
            var rawResource = new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "/anything" };
            var expectedOutputResource = new Resource { ParentUri = rawResource.ParentUri, Uri = new Uri("http://www.helix.com/anything") };
            AddTheoryDescription(rawResource, expectedOutputResource, true);
        }

        void CreateResourceFromRawResource()
        {
            var rawResource = new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.helix.com/anything" };
            var expectedOutputResource = new Resource { ParentUri = rawResource.ParentUri, Uri = new Uri(rawResource.Url) };
            AddTheoryDescription(rawResource, expectedOutputResource, true);
        }

        void ThrowExceptionIfArgumentIsNotValid()
        {
            var rawResource = new RawResource { ParentUri = null, Url = "/anything" };
            AddTheoryDescription(rawResource, p4: typeof(ArgumentException));
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p4: typeof(ArgumentNullException)); }
    }
}