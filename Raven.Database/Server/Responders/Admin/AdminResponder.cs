﻿using Raven.Database.Extensions;
using Raven.Database.Server.Abstractions;

namespace Raven.Database.Server.Responders.Admin
{
	public abstract class AdminResponder : RequestResponder
	{
		public override string UrlPattern
		{
			get { return "^/admin/"+ GetType().Name.Replace("Admin", "") +"$"; }
		}

		public override string[] SupportedVerbs
		{
			get { return new[] { "POST" }; }
		}

		public override void Respond(Abstractions.IHttpContext context)
		{
			if (context.User.IsAdministrator() == false)
			{
				context.SetStatusToUnauthorized();
				context.WriteJson(new
				{
					Error = "The operation '" + context.GetRequestUrl() +"' is only available to administrators"
				});
				return;
			}

			RespondToAdmin(context);
		}

		public abstract void RespondToAdmin(IHttpContext context);
	}
}