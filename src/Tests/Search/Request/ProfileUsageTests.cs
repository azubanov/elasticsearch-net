﻿using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Nest;
using Tests.Framework;
using Tests.Framework.Integration;
using Tests.Framework.MockData;

namespace Tests.Search.Request
{
	public class ProfileUsageTests : SearchUsageTestBase
	{
		public ProfileUsageTests(ReadOnlyCluster cluster, EndpointUsage usage) : base(cluster, usage) { }

		protected override object ExpectJson => new
		{
			profile = true,
			query = new
			{
				term = new { name = new { value = "elasticsearch" } }
			}
		};

		protected override Func<SearchDescriptor<Project>, ISearchRequest> Fluent => s => s
			.Profile()
			.Query(q => q
				.Term(p => p.Name, "elasticsearch")
			);

		protected override SearchRequest<Project> Initializer =>
			new SearchRequest<Project>
			{
				Profile = true,
				Query = new TermQuery
				{
					Field = "name",
					Value = "elasticsearch"
				}
			};

		[I] public async Task ProfileResults() => await AssertOnAllResponses((r) =>
		{
			r.Profile.Should().NotBeNull();
			r.Profile.Shards.Should().NotBeNullOrEmpty();

			var shard = r.Profile.Shards.First();
			shard.Id.Should().NotBeNullOrWhiteSpace();
			shard.Searches.Should().NotBeNullOrEmpty();

			var firstSearch = shard.Searches.First();
			firstSearch.RewriteTime.Should().BeGreaterThan(0);
			firstSearch.Query.Should().NotBeNullOrEmpty();
			firstSearch.Collector.Should().NotBeNullOrEmpty();

			var firstQuery = firstSearch.Query.First();
			firstQuery.Breakdown.Should().NotBeNull();
			firstQuery.Breakdown.CreateWeight.Should().BeGreaterThan(0);

			firstQuery.Lucene.Should().NotBeNullOrWhiteSpace();
			firstQuery.QueryType.Should().NotBeNullOrWhiteSpace();
			firstQuery.Time.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(0.000001));
			firstQuery.Children.Should().NotBeNullOrEmpty();

			var firstCollector = firstSearch.Collector.First();
			firstCollector.Name.Should().NotBeNullOrEmpty();
			firstCollector.Reason.Should().NotBeNullOrEmpty();
			firstCollector.Time.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(0.000001));




		});
	}
}
