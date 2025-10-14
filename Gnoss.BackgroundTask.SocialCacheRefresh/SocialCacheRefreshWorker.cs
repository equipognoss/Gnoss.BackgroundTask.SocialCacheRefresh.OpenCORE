using Es.Riam.Gnoss.Elementos.Suscripcion;
using Es.Riam.Gnoss.Servicios;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Win.RefrescoCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gnoss.BackgroundTask.SocialCacheRefresh
{
    public class SocialCacheRefreshWorker : Worker
    {
        private readonly ConfigService _configService;
        private ILogger mlogger;
        private ILoggerFactory mLoggerFactory;

        public SocialCacheRefreshWorker(ConfigService configService, IServiceScopeFactory scopeFactory, ILogger<SocialCacheRefreshWorker> logger, ILoggerFactory loggerFactory) 
            : base(logger, scopeFactory)
        {
            _configService = configService;
            mlogger = logger;
            mLoggerFactory = loggerFactory;
        }

        protected override List<ControladorServicioGnoss> ObtenerControladores()
        {
            List<ControladorServicioGnoss> controladores = new List<ControladorServicioGnoss>();
            int numMaxPeticionesWebSimultaneas = _configService.ObtenerNumMaxPeticionesWebSimultaneas();

            controladores.Add(new ControladorRefrescoCache(numMaxPeticionesWebSimultaneas, ScopedFactory, _configService, mLoggerFactory.CreateLogger<ControladorRefrescoCache>(), mLoggerFactory));
            return controladores;
        }
    }
}
