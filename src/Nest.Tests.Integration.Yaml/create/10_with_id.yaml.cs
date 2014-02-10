using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest;
using NUnit.Framework;


namespace Nest.Tests.Integration.Yaml.Create
{
	public partial class Create10WithIdYaml10Tests
	{
		
		public class CreateWithId10Tests
		{
			private readonly RawElasticClient _client;
		
			public CreateWithId10Tests()
			{
				var uri = new Uri("http:localhost:9200");
				var settings = new ConnectionSettings(uri, "nest-default-index");
				_client = new RawElasticClient(settings);
			}

			[Test]
			public void CreateWithIdTests()
			{

				//do create 
				this._client.IndexPost("test_1", "test", "1", "SERIALIZED BODY HERE", nv=>nv);

				//do get 
				this._client.Get("test_1", "test", "1", nv=>nv);

				//do create 
				this._client.IndexPost("test_1", "test", "1", "SERIALIZED BODY HERE", nv=>nv);
			}
		}
	}
}