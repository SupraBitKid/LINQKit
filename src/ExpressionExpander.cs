using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Collections.ObjectModel;
using System.Reflection;

namespace LinqKit
{
	/// <summary>
	/// Custom expression visitor for ExpandableQuery. This expands calls to Expression.Compile() and
	/// collapses captured lambda references in subqueries which LINQ to SQL can't otherwise handle.
	/// </summary>
	class ExpressionExpander : ExpressionVisitor
	{
		// Replacement parameters - for when invoking a lambda expression.
		readonly Dictionary<ParameterExpression, Expression> _replaceVars = null;

		internal ExpressionExpander () { }

		private ExpressionExpander (Dictionary<ParameterExpression, Expression> replaceVars)
		{
			_replaceVars = replaceVars;
		}

		protected override Expression VisitParameter (ParameterExpression p)
		{
			return (_replaceVars != null) && (_replaceVars.ContainsKey(p)) ? _replaceVars[p] : base.VisitParameter(p);
		}

		/// <summary>
		/// Flatten calls to Invoke so that Entity Framework can understand it. Calls to Invoke are generated
		/// by PredicateBuilder.
		/// </summary>
		protected override Expression VisitInvocation (InvocationExpression iv)
		{
			var target = iv.Expression;
			if (target is MemberExpression) target = TransformExpression ((MemberExpression)target);
			if (target is ConstantExpression) target = ((ConstantExpression)target).Value as Expression;

			var lambda = (LambdaExpression)target;

			var replaceVars = _replaceVars == null ? 
				new Dictionary<ParameterExpression, Expression> () 
				: new Dictionary<ParameterExpression, Expression> (_replaceVars);

			try
			{
				for (int i = 0; i < lambda.Parameters.Count; i++)
					replaceVars.Add (lambda.Parameters[i], iv.Arguments[i]);
			}
			catch (ArgumentException ex)
			{
				throw new InvalidOperationException ("Invoke cannot be called recursively - try using a temporary variable.", ex);
			}

			return new ExpressionExpander (replaceVars).Visit (lambda.Body);
		}

		protected override Expression VisitMethodCall (MethodCallExpression m)
		{
			if (m.Method.Name == "Invoke" && m.Method.DeclaringType == typeof (Extensions))
			{
				var target = m.Arguments[0];
				if (target is MemberExpression) 
					target = TransformExpression ((MemberExpression)target);
				if (target is ConstantExpression) 
					target = ((ConstantExpression) target).Value as Expression;


				var lambda = (LambdaExpression)target;

				var replaceVars = _replaceVars == null ? 
					new Dictionary<ParameterExpression, Expression> () 
					: new Dictionary<ParameterExpression, Expression> (_replaceVars);

				try
				{
					for (int i = 0; i < lambda.Parameters.Count; i++)
						replaceVars.Add (lambda.Parameters[i], m.Arguments[i + 1]);
				}
				catch (ArgumentException ex)
				{
					throw new InvalidOperationException ("Invoke cannot be called recursively - try using a temporary variable.", ex);
				}

				return new ExpressionExpander (replaceVars).Visit (lambda.Body);
			}

			// Expand calls to an expression's Compile() method:
			if (m.Method.Name == "Compile" && m.Object is MemberExpression)
			{
				var me = (MemberExpression)m.Object;
				var newExpr = TransformExpression (me);
				if (newExpr != me) return newExpr;
			}

			// Strip out any nested calls to AsExpandable():
			if (m.Method.Name == "AsExpandable" && m.Method.DeclaringType == typeof (Extensions))
				return m.Arguments[0];

			return base.VisitMethodCall (m);
		}

		protected override Expression VisitMemberAccess (MemberExpression m)
		{
			// Strip out any references to expressions captured by outer variables - LINQ to SQL can't handle these:
			return m.Member.DeclaringType.Name.StartsWith ("<>") ? 
				TransformExpression (m) 
				: base.VisitMemberAccess (m);
		}

		Expression TransformExpression(MemberExpression input)
		{
			if (input == null)
				return null;

			switch( input.Member.MemberType ) {
				case MemberTypes.Property:
					return this.TransformPropertyExpression( input );

				case MemberTypes.Field:
					return this.TransformFieldExpression( input );
			}

			return input;
		}

		Expression TransformPropertyExpression( MemberExpression input)
		{
			if (input == null)
				return null;

			var propertyInfo = input.Member as PropertyInfo;

			if( propertyInfo == null )
				return input;

			// Collapse captured outer variables
			if( !input.Member.ReflectedType.IsNestedPrivate || !input.Member.ReflectedType.Name.StartsWith( "<>" ) ) {
				// captured outer variable 
				return TryVisitExpressionFunc( input, propertyInfo );
			}

			var expression = input.Expression as ConstantExpression;
			if (expression != null) {
				var expValue = expression.Value;
				if (expValue == null) 
					return input;
				var expType = expValue.GetType();
				if (!expType.IsNestedPrivate || !expType.Name.StartsWith("<>")) 
					return input;

				object result = propertyInfo.GetValue(expValue);

				var exp = result as Expression;

				if (exp != null) 
					return Visit(exp);
			}

			return TryVisitExpressionFunc(input, propertyInfo);
		}

		Expression TransformFieldExpression( MemberExpression input ) {
			if (input == null)
				return null;

			var fieldInfo = input.Member as FieldInfo;

			if( fieldInfo == null )
				return input;

			// Collapse captured outer variables
			if( !input.Member.ReflectedType.IsNestedPrivate || !input.Member.ReflectedType.Name.StartsWith( "<>" ) ) {
				// captured outer variable 
				return TryVisitExpressionFunc( input, fieldInfo );
			}

			var expression = input.Expression as ConstantExpression;

			if (expression != null) {
				var expValue = expression.Value;
				if (expValue == null) 
					return input;
				var expType = expValue.GetType();
				if (!expType.IsNestedPrivate || !expType.Name.StartsWith("<>")) 
					return input;

				object result = fieldInfo.GetValue(expValue);

				var exp = result as Expression;

				if (exp != null) 
					return Visit(exp);
			}

			return TryVisitExpressionFunc(input, fieldInfo);
		}

		private Expression TryVisitExpressionFunc( MemberExpression input, PropertyInfo property ) {
			if( property.PropertyType.IsSubclassOf( typeof( Expression ) ) ) 
				return Visit( Expression.Lambda<Func<Expression>>( input ).Compile()() );

			return input;
		}

		private Expression TryVisitExpressionFunc(MemberExpression input, FieldInfo field)
		{
			var prope = input.Member as PropertyInfo;
			if ((field.FieldType.IsSubclassOf(typeof (Expression))) ||
				(prope != null && prope.PropertyType.IsSubclassOf(typeof (Expression))))
				return Visit(Expression.Lambda<Func<Expression>>(input).Compile()());

			return input;
		}
	}
}
