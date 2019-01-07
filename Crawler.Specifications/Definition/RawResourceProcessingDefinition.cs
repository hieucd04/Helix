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
            ThrowExceptionIfArgumentNull();
        }

        void CreateResourceFromRawResource()
        {
            var rawResource = new RawResource { ParentUrl = "http://www.helix.com", Url = "http://www.helix.com/anything" };
            var expectedOutputResource = new Resource { ParentUri = new Uri(rawResource.ParentUrl), Uri = new Uri(rawResource.Url) };
            AddTheoryDescription(rawResource, expectedOutputResource, true);
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p4: typeof(ArgumentNullException)); }
    }
}