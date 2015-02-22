using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;
using LinqKit;

namespace LinqKit.Tests {
    public class ExpressionExpanderTest {
		
		#region class level declarations
		private class InnerDTOClass {
			public string String1 { get; set; }
		}

		private class OuterDTOClass {
			public string String2 { get; set; }
			public InnerDTOClass Class1 { get; set; }
		}

		private IQueryable<string> GetQueriableData() {
			List<string> testData = new List<string>();
			testData.Add( Guid.NewGuid().ToString() );
			testData.Add( Guid.NewGuid().ToString() );

			return testData.AsQueryable();
		}
		#endregion class level declarations
		
		#region tests
		[Fact]
        public void NestedMappingSelectorInline() {
			var testQueryable = this.GetQueriableData().AsExpandable();

			var query = testQueryable.Select(
				( entity ) => new OuterDTOClass() {
					String2 = entity.ToUpper(),
					Class1 = new InnerDTOClass() {
						String1 = entity.ToLower()
					}
				} );

			var result = query.ToList();

			Assert.NotNull( result );
			Assert.Equal( testQueryable.Count(), result.Count );
		}

		[Fact]
		public void NestedMappingSelectorAsOneLocalVariable() {
			var testQueryable = this.GetQueriableData().AsExpandable();

			Expression<Func<string,OuterDTOClass>> outerClassMap = ( entity ) => new OuterDTOClass() {
				String2 = entity.ToUpper(),
				Class1 = new InnerDTOClass() {
					String1 = entity.ToLower()
				}
			};

			var query = testQueryable.Select( outerClassMap );

			var result = query.ToList();

			Assert.NotNull( result );
			Assert.Equal( testQueryable.Count(), result.Count );
		}

		[Fact]
		public void NestedMappingSelectorAsTwoLocalVariables() {
			var testQueryable = this.GetQueriableData().AsExpandable();

			Expression<Func<string,InnerDTOClass>> innerClassMap = ( entity ) => new InnerDTOClass() {
				String1 = entity.ToLower()
			};

			Expression<Func<string,OuterDTOClass>> outerClassMap = ( entity ) => new OuterDTOClass() {
				String2 = entity.ToUpper(),
				Class1 = innerClassMap.Invoke( entity )
			};

			var query = testQueryable.Select( outerClassMap );

			var result = query.ToList();

			Assert.NotNull( result );
			Assert.Equal( testQueryable.Count(), result.Count );
		}


		private static Expression<Func<string,OuterDTOClass>> OuterClassMapStatic1 = ( entity ) => new OuterDTOClass() {
			String2 = entity.ToUpper(),
			Class1 = new InnerDTOClass() {
				String1 = entity.ToLower()
			}
		};


		[Fact]
		public void NestedMappingSelectorAsOneClassField() {
			var testQueryable = this.GetQueriableData();

			var query = testQueryable.Select( OuterClassMapStatic1 );

			var result = query.ToList();

			Assert.NotNull( result );
			Assert.Equal( testQueryable.Count(), result.Count );
		}

		private static Expression<Func<string,InnerDTOClass>> InnerClassMapStatic1 = ( entity ) => new InnerDTOClass() {
			String1 = entity.ToLower()
		};

		private static Expression<Func<string,OuterDTOClass>> OuterClassMapStatic2 = ( entity ) => new OuterDTOClass() {
			String2 = entity.ToUpper(),
			Class1 = InnerClassMapStatic1.Invoke( entity )
		};

		[Fact]
		public void NestedMappingSelectorAsTwoClassFields() {
			var testQueryable = this.GetQueriableData().AsExpandable();

			var query = testQueryable.Select( OuterClassMapStatic2 );

			var result = query.ToList();

			Assert.NotNull( result );
			Assert.Equal( testQueryable.Count(), result.Count );
		}

		private static Expression<Func<string,OuterDTOClass>> OuterClassMapStatic3 {
			get {
				return ( entity ) => new OuterDTOClass() {
					String2 = entity.ToUpper(),
					Class1 = InnerClassMapStatic1.Invoke( entity )
				};
			}
		}

		[Fact]
		public void NestedMappingSelectorAsOneClassProperty() {
			var testQueryable = this.GetQueriableData().AsExpandable();

			var query = testQueryable.Select( OuterClassMapStatic3 );

			var result = query.ToList();

			Assert.NotNull( result );
			Assert.Equal( testQueryable.Count(), result.Count );
		}

		private static Expression<Func<string, InnerDTOClass>> InnerClassMapStatic2 {
			get {
				return ( entity ) => new InnerDTOClass() {
					String1 = entity.ToLower()
				};
			}
		}

		private static Expression<Func<string, OuterDTOClass>> OuterClassMapStatic4 {
			get {
				return ( entity ) => new OuterDTOClass() {
					String2 = entity.ToUpper(),
					Class1 = InnerClassMapStatic2.Invoke( entity )
				};
			}
		}

		[Fact]
		public void NestedMappingSelectorAsTwoClassProperties() {
			var testQueryable = this.GetQueriableData().AsExpandable();

			var query = testQueryable.Select( OuterClassMapStatic4 );

			var result = query.ToList();

			Assert.NotNull( result );
			Assert.Equal( testQueryable.Count(), result.Count );
			for( int index = 0; index < testQueryable.Count(); index++ ) {
				Assert.Equal( testQueryable.ElementAt( index ).ToUpper(), result[ index ].String2 );

				Assert.NotNull( result[ index ].Class1 );

				Assert.Equal( testQueryable.ElementAt( index ).ToLower(), result[ index ].Class1.String1 );
			}
		}

		private static Expression<Func<string, OuterDTOClass>> OuterClassMapStatic5 {
			get {
				return ( entity ) => new OuterDTOClass() {
					String2 = entity,
					Class1 = InnerClassMapStatic2.Invoke( entity )
				};
			}
		}

		[Fact]
		public void NestedMappingSelectorAsTwoClassPropertiesOneReuse() {
			var testQueryable = this.GetQueriableData().AsExpandable();

			var query = testQueryable.Select( OuterClassMapStatic5 );

			var result = query.ToList();

			Assert.NotNull( result );
			Assert.Equal( testQueryable.Count(), result.Count );
			for( int index = 0; index < testQueryable.Count(); index++ ) {
				Assert.Equal( testQueryable.ElementAt( index ), result[ index ].String2 );

				Assert.NotNull( result[ index ].Class1 );

				Assert.Equal( testQueryable.ElementAt( index ).ToLower(), result[ index ].Class1.String1 );
			}
		}

		#endregion tests
	}
}
