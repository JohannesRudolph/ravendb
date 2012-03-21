﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using RavenFS.Controllers;
using RavenFS.Storage;
using SignalR;
using SignalR.Infrastructure;

namespace RavenFS.Notifications
{
    public class NotificationPublisher
    {
        private readonly IDependencyResolver dependencyResolver;
        private readonly IConnection connection ;

        public NotificationPublisher()
        {
            dependencyResolver = new DefaultDependencyResolver();
            connection = dependencyResolver.Resolve<IConnectionManager>().GetConnection<NotificationEndpoint>();
        }

        public IDependencyResolver SignalRDependencyResolver
        {
            get { return dependencyResolver; }
        }

        public void Publish(FileChange change)
        {
            connection.Broadcast(change);
        }
    }
}