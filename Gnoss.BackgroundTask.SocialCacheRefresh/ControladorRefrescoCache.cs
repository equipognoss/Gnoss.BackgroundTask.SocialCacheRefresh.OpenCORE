using System;
using System.Collections.Generic;
using System.Threading;
using System.Data;
using Es.Riam.Util;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.Logica.Live;
using Es.Riam.Gnoss.Logica.Identidad;
using Es.Riam.Gnoss.Recursos;
using Es.Riam.Gnoss.AD.ServiciosGenerales;
using Es.Riam.Gnoss.Logica.ServiciosGenerales;
using Es.Riam.Gnoss.Logica.BASE_BD;
using Es.Riam.Gnoss.AD.BASE_BD.Model;
using Es.Riam.Gnoss.AD.Facetado;
using Es.Riam.Gnoss.Servicios;
using Es.Riam.Gnoss.AD.BASE_BD;
using Es.Riam.Gnoss.AD.Tags;
using Es.Riam.Gnoss.Servicios.ControladoresServiciosWeb;
using System.Linq;
using Es.Riam.Gnoss.RabbitMQ;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.Elementos.ParametroAplicacion;
using Es.Riam.Gnoss.Web.Controles.ParametroAplicacionGBD;
using Es.Riam.AbstractsOpen;

namespace Es.Riam.Gnoss.Win.RefrescoCache
{
    internal class ControladorRefrescoCache : ControladorServicioGnoss
    {
        #region Constantes

        private const string COLA_REFRESCO_CACHE = "ColaRefrescoCacheMensajes";
        private const string EXCHANGE = "";

        #endregion

        #region Miembros

        private List<string> mIdiomasList = new List<string>();

        private int mNumeroMaxPeticionesWebSimultaneas = 5;
        

        #endregion

        #region Constructores

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pFicheroConfiguracionSitioWeb">Ruta al archivo de configuración del sitio Web</param>
        public ControladorRefrescoCache(int pNumeroMaxPeticionesWebSimultaneas, IServiceScopeFactory scopedFactory, ConfigService configService)
            : base(scopedFactory, configService)
        {
            mNumeroMaxPeticionesWebSimultaneas = pNumeroMaxPeticionesWebSimultaneas;

            CargarIdiomas();
        }


        /// <summary>
        /// Cargamos los idiomas disponibles para la plataforma web.
        /// </summary>
        private void CargarIdiomas()
        {
            mIdiomasList.Add("es");
            mIdiomasList.Add("en");
            mIdiomasList.Add("pt");
        }

        #endregion

        #region Métodos generales

        public override void RealizarMantenimiento(EntityContext entityContext, EntityContextBASE entityContextBASE, UtilidadesVirtuoso utilidadesVirtuoso, LoggingService loggingService, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, VirtuosoAD virtuosoAD, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            GestorParametroAplicacionDS = new GestorParametroAplicacion();
            ParametroAplicacionGBD parametroAplicacionGBD = new ParametroAplicacionGBD(loggingService, entityContext, mConfigService);
            parametroAplicacionGBD.ObtenerConfiguracionGnoss(GestorParametroAplicacionDS);
            mUrlIntragnoss = GestorParametroAplicacionDS.ParametroAplicacion.Where(parametroAplicacion=>parametroAplicacion.Parametro.Equals("UrlIntragnoss")).FirstOrDefault().Valor;

            #region Establezco el dominio de la cache

            //mDominio = ((ParametroAplicacionDS.ParametroAplicacionRow)ParametroAplicacionDS.ParametroAplicacion.Select("Parametro='UrlIntragnoss'")[0]).Valor;
            mDominio = GestorParametroAplicacionDS.ParametroAplicacion.Where(parametroAplicacion => parametroAplicacion.Parametro.Equals("UrlIntragnoss")).FirstOrDefault().Valor;
            mDominio = mDominio.Replace("http://", "").Replace("www.", "");

            if (mDominio[mDominio.Length - 1] == '/')
            {
                mDominio = mDominio.Substring(0, mDominio.Length - 1);
            }

            #endregion

            #region Cargo los idiomas del ecosistema

            //Carga de los idiomas del ecosistema en un diccionario.
            string[] separador = new string[] { "&&&" };
            //string[] idiomasConfigurados = ParametroAplicacionDS.ParametroAplicacion.Select("Parametro = 'Idiomas'")[0]["Valor"].ToString().Split(separador, StringSplitOptions.RemoveEmptyEntries);
            string[] idiomasConfigurados = GestorParametroAplicacionDS.ParametroAplicacion.Where(parametroAplicacion=>parametroAplicacion.Parametro.Equals("Idiomas")).FirstOrDefault().Valor.Split(separador, StringSplitOptions.RemoveEmptyEntries);

            foreach (string idioma in idiomasConfigurados)
            {
                mListaIdiomasEcosistema.Add(idioma.Split('|')[0], idioma.Split('|')[1]);
            }

            #endregion

            RealizarMantenimientoRabbitMQ(loggingService);
            //RealizarMantenimientoBD();
        }

        private void RealizarMantenimientoBD()
        {
            while (true)
            {
                using (var scope = ScopedFactory.CreateScope())
                {
                    EntityContext entityContext = scope.ServiceProvider.GetRequiredService<EntityContext>();
                    EntityContextBASE entityContextBASE = scope.ServiceProvider.GetRequiredService<EntityContextBASE>();
                    UtilidadesVirtuoso utilidadesVirtuoso = scope.ServiceProvider.GetRequiredService<UtilidadesVirtuoso>();
                    LoggingService loggingService = scope.ServiceProvider.GetRequiredService<LoggingService>();
                    VirtuosoAD virtuosoAD = scope.ServiceProvider.GetRequiredService<VirtuosoAD>();
                    RedisCacheWrapper redisCacheWrapper = scope.ServiceProvider.GetRequiredService<RedisCacheWrapper>();
                    UtilPeticion utilPeticion = scope.ServiceProvider.GetRequiredService<UtilPeticion>();
                    IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication = scope.ServiceProvider.GetRequiredService<IServicesUtilVirtuosoAndReplication>();
                    try
                    {
                        ComprobarCancelacionHilo();

                        if (mReiniciarLecturaRabbit)
                        {
                            RealizarMantenimientoRabbitMQ(loggingService);
                        }

                        BaseComunidadCN baseComunidadCN = new BaseComunidadCN(entityContext, loggingService, entityContextBASE, mConfigService, servicesUtilVirtuosoAndReplication);
                        baseComunidadCN.EliminarColaRefrescoCachePendientesRepetidas();
                        BaseComunidadDS baseComunidadDS = baseComunidadCN.ObtenerColaRefrescoCacheBandejaMensajesPendientes();

                        foreach (BaseComunidadDS.ColaRefrescoCacheRow filaCola in baseComunidadDS.ColaRefrescoCache.Rows)
                        {
                            ProcesarFilasDeColaRefrescoCache(filaCola, entityContext, loggingService, virtuosoAD, utilPeticion, utilidadesVirtuoso, servicesUtilVirtuosoAndReplication);

                            baseComunidadCN.AcutalizarEstadoColaRefrescoCache(filaCola.ColaID, filaCola.Estado);

                        }

                        ComprobarCancelacionHilo();
                    }
                    catch (ThreadAbortException) { }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        loggingService.GuardarLog("ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                        ControladorConexiones.CerrarConexiones();
                    }
                    finally
                    {
                        //Duermo el proceso el tiempo establecido
                        Thread.Sleep(INTERVALO_SEGUNDOS * 1000);
                    }
                }
            }
            ControladorConexiones.CerrarConexiones();
        }

        private void RealizarMantenimientoRabbitMQ(LoggingService loggingService, bool reintentar = true)
        {
            if (mConfigService.ExistRabbitConnection(RabbitMQClient.BD_SERVICIOS_WIN))
            {
                RabbitMQClient.ReceivedDelegate funcionProcesarItem = new RabbitMQClient.ReceivedDelegate(ProcesarItem);
                RabbitMQClient.ShutDownDelegate funcionShutDown = new RabbitMQClient.ShutDownDelegate(OnShutDown);
                
                RabbitMQClient rabbitMQClient = new RabbitMQClient(RabbitMQClient.BD_SERVICIOS_WIN, COLA_REFRESCO_CACHE, loggingService, mConfigService, EXCHANGE, COLA_REFRESCO_CACHE);

                try
                {
                    rabbitMQClient.ObtenerElementosDeCola(funcionProcesarItem, funcionShutDown);
                    mReiniciarLecturaRabbit = false;
                }
                catch (Exception ex)
                {
                    mReiniciarLecturaRabbit = true;
                    loggingService.GuardarLogError(ex);
                }
            }
        }
        
        private bool ProcesarItem(string pFila)
        {
            using (var scope = ScopedFactory.CreateScope())
            {
                EntityContext entityContext = scope.ServiceProvider.GetRequiredService<EntityContext>();
                UtilidadesVirtuoso utilidadesVirtuoso = scope.ServiceProvider.GetRequiredService<UtilidadesVirtuoso>();
                LoggingService loggingService = scope.ServiceProvider.GetRequiredService<LoggingService>();
                VirtuosoAD virtuosoAD = scope.ServiceProvider.GetRequiredService<VirtuosoAD>();
                UtilPeticion utilPeticion = scope.ServiceProvider.GetRequiredService<UtilPeticion>();
                IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication = scope.ServiceProvider.GetRequiredService<IServicesUtilVirtuosoAndReplication>();
                try
                {
                    ComprobarCancelacionHilo();

                    System.Diagnostics.Debug.WriteLine($"ProcesarItem, {pFila}!");

                    if (!string.IsNullOrEmpty(pFila))
                    {
                        object[] itemArray = JsonConvert.DeserializeObject<object[]>(pFila);
                        BaseComunidadDS.ColaRefrescoCacheRow filaCola = (BaseComunidadDS.ColaRefrescoCacheRow)new BaseComunidadDS().ColaRefrescoCache.Rows.Add(itemArray);
                        itemArray = null;

                        ProcesarFilasDeColaRefrescoCache(filaCola, entityContext, loggingService, virtuosoAD, utilPeticion, utilidadesVirtuoso, servicesUtilVirtuosoAndReplication);

                        filaCola = null;

                        servicesUtilVirtuosoAndReplication.ConexionAfinidad = "";

                        ControladorConexiones.CerrarConexiones(false);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    loggingService.GuardarLogError(ex);
                    return true;
                }
            }
        }

        private void ProcesarFilasDeColaRefrescoCache(BaseComunidadDS.ColaRefrescoCacheRow pColaRefrescoCache, EntityContext entityContext, LoggingService loggingService, VirtuosoAD virtuosoAD, UtilPeticion utilPeticion, UtilidadesVirtuoso utilidadesVirtuoso, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            ComprobarCancelacionHilo();

            ProcesarFila(pColaRefrescoCache, entityContext, loggingService, virtuosoAD, utilPeticion, utilidadesVirtuoso, servicesUtilVirtuosoAndReplication);

            if (pColaRefrescoCache.Estado == 0)
            {
                //Procesada correctamente
                pColaRefrescoCache.Estado = 2;
                //baseComunidadCN.EliminarFilaColaRefrescoCache(filaCola.ColaID);
            }
        }

        private void ProcesarFila(BaseComunidadDS.ColaRefrescoCacheRow pFilaCola, EntityContext entityContext, LoggingService loggingService, VirtuosoAD virtuosoAD, UtilPeticion utilPeticion, UtilidadesVirtuoso utilidadesVirtuoso, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            switch (pFilaCola.TipoEvento)
            {
                case (short)TiposEventosRefrescoCache.CambiosBandejaDeMensajes:
                    try
                    {
                        ActualizarCachesRemitenteYDestinatarios(pFilaCola, entityContext, loggingService, utilPeticion, utilidadesVirtuoso, virtuosoAD, servicesUtilVirtuosoAndReplication);
                    }
                    catch (ThreadAbortException) { }
                    catch (Exception ex)
                    {
                        pFilaCola.Estado = 1;
                        EnviarCorreoErrorYGuardarLog(ex, "Error Refresco caché (ProcesarFilaDeComponentes)", entityContext, loggingService);
                    }
                    break;
                case (short)TiposEventosRefrescoCache.RecalcularContadoresPerfil:
                    Guid perfilID;
                    try
                    {
                        if (Guid.TryParse(pFilaCola.InfoExtra, out perfilID))
                        {
                            EstablecerNumeroMensajesSinLeer(perfilID, entityContext, loggingService, virtuosoAD, servicesUtilVirtuosoAndReplication);
                        }
                        else
                        {
                            pFilaCola.Estado = 1;
                        }
                    }
                    catch (ThreadAbortException) { }
                    catch (Exception ex)
                    {
                        pFilaCola.Estado = 1;
                        EnviarCorreoErrorYGuardarLog(ex, "Error Refresco caché (ProcesarFilaDeComponentes)", entityContext, loggingService);
                    }
                    break;
            }
        }

        /// <summary>
        /// Actualización de la caché de la bandeja de recibidos, enviados, eliminados para cada mensaje nuevo.
        /// </summary>
        /// <param name="pFilaCola">Fila de la BD a procesar.</param>
        private void ActualizarCachesRemitenteYDestinatarios(BaseComunidadDS.ColaRefrescoCacheRow pFilaCola, EntityContext entityContext, LoggingService loggingService, UtilPeticion utilPeticion, UtilidadesVirtuoso utilidadesVirtuoso, VirtuosoAD virtuosoAD, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            // Cargar los datos del proyecto
            string urlPropiaProyecto = string.Empty;
            ProyectoCN proyCN = new ProyectoCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
            urlPropiaProyecto = proyCN.ObtenerURLPropiaProyecto(pFilaCola.ProyectoID);
            proyCN.Dispose();

            CargadorResultados cargadorResultadosHome = new CargadorResultados();
            cargadorResultadosHome.Url = mConfigService.ObtenerUrlServicioResultados();

            CargadorFacetas cargadorFacetasHome = new CargadorFacetas();
            cargadorFacetasHome.Url = mConfigService.ObtenerUrlServicioFacetas();
            utilPeticion.AgregarObjetoAPeticionActual("UsarMasterParaLectura", true);

            // Actualizar la caché de facetas y resultados para las bandejas de enviados y recibidos.
            Dictionary<short, List<string>> listaTagsFiltros = ObtenerTagsFiltros(pFilaCola.InfoExtra);
            List<Guid> listaPerfilesDestinatarios = new List<Guid>();

            // Por cada idioma debemos calculas las cachés de las bandejas del usuario.
            IdentidadCN identCN = new IdentidadCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
            PersonaCN personaCN = new PersonaCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);

            Guid identidadID = new Guid();

            string[] delimiter = { "|" };

            Dictionary<string, string> dicPerfilOrganizacionIdioma = new Dictionary<string, string>();

            if (Guid.TryParse(pFilaCola.InfoExtra, out identidadID))
            {
                if (!identidadID.Equals(Guid.Empty))
                {
                    ActualizarCachePorIdentidad(identidadID, dicPerfilOrganizacionIdioma, cargadorResultadosHome, cargadorFacetasHome, pFilaCola, identCN, personaCN, loggingService, utilidadesVirtuoso);
                }
            }
            //if (listaTagsFiltros.Count > 0 && listaTagsFiltros[0].Count > 0)
            else
            {
                //Proviene del servicio del modulo base usuario.

                #region Mensajes_TO

                foreach (string identidadID_mensaje_to_entubados in listaTagsFiltros[(short)TiposTags.IDTagMensajeTo])
                {
                    foreach (string identidadID_mensaje_to in identidadID_mensaje_to_entubados.Split(delimiter, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (identidadID_mensaje_to.StartsWith("g_"))
                        {
                            Guid idGrupo = new Guid(identidadID_mensaje_to.Substring(2));
                            List<Guid> identidadesGrupo = identCN.ObtenerParticipantesGrupo(idGrupo);
                            foreach (Guid identidadParticipante in identidadesGrupo)
                            {
                                Guid? perfilID = ActualizarCachePorIdentidad(identidadParticipante, dicPerfilOrganizacionIdioma, cargadorResultadosHome, cargadorFacetasHome, pFilaCola, identCN, personaCN, loggingService, utilidadesVirtuoso);
                                if (perfilID.HasValue && !listaPerfilesDestinatarios.Contains(perfilID.Value))
                                {
                                    listaPerfilesDestinatarios.Add(perfilID.Value);
                                }
                            }
                        }
                        else
                        {
                            Guid? perfilID = ActualizarCachePorIdentidad(new Guid(identidadID_mensaje_to), dicPerfilOrganizacionIdioma, cargadorResultadosHome, cargadorFacetasHome, pFilaCola, identCN, personaCN, loggingService, utilidadesVirtuoso);
                            if (perfilID.HasValue && !listaPerfilesDestinatarios.Contains(perfilID.Value))
                            {
                                listaPerfilesDestinatarios.Add(perfilID.Value);
                            }
                        }
                    }
                }

                #endregion

                #region Mensajes_From

                foreach (string id_mensaje_from_entubados in listaTagsFiltros[(short)TiposTags.IDTagMensajeFrom])
                {
                    foreach (string id_mensaje_from in id_mensaje_from_entubados.Split(delimiter, StringSplitOptions.RemoveEmptyEntries))
                    {
                        // En el caso de los registros, el mensaje proviene de un Guid vacio '00000...'
                        if (!Guid.Empty.Equals(new Guid(id_mensaje_from)))
                        {
                            ActualizarCachePorIdentidad(new Guid(id_mensaje_from), dicPerfilOrganizacionIdioma, cargadorResultadosHome, cargadorFacetasHome, pFilaCola, identCN, personaCN, loggingService, utilidadesVirtuoso);
                        }
                    }
                }

                #endregion
            }

            identCN.Dispose();

            // Por ultimo agregamos la notificación de correo nuevo para las identidades que lo han recibido.
            if (listaPerfilesDestinatarios.Count > 0)
            {
                AgregarNotificacionCorreoNuevoAIdentidades(listaPerfilesDestinatarios, entityContext, loggingService, virtuosoAD, servicesUtilVirtuosoAndReplication);
            }
        }

        private Guid? ActualizarCachePorIdentidad(Guid pIdentidadID, Dictionary<string, string> dicPerfilOrganizacionIdioma, CargadorResultados cargadorResultadosHome, CargadorFacetas cargadorFacetasHome, BaseComunidadDS.ColaRefrescoCacheRow pFilaCola, IdentidadCN identCN, PersonaCN personaCN, LoggingService loggingService, UtilidadesVirtuoso utilidadesVirtuoso)
        {
            Guid usuarioID = identCN.ObtenerUsuarioIDConIdentidadID(pIdentidadID);

            Guid? orgID = null;
            orgID = identCN.ObtenerOrganizacionIDConIdentidadID(pIdentidadID);
            if (usuarioID.Equals(Guid.Empty))
            {
                if (orgID.HasValue)
                {
                    usuarioID = orgID.Value;
                }
            }

            Es.Riam.Gnoss.AD.EntityModel.Models.PersonaDS.Persona personaRow = personaCN.ObtenerPersonaPorIdentidadCargaLigera(pIdentidadID);
            string idioma = "es";
            if (personaRow != null && !string.IsNullOrEmpty(personaRow.Idioma))
            {
                idioma = personaRow.Idioma;
            }

            //Agregamos el perfil y la organización para limpiar sus cachés después
            Guid? perfilID = identCN.ObtenerPerfilIDDeIdentidadID(pIdentidadID);
            string cadena = "";
            if (perfilID.HasValue)
            {
                cadena = perfilID.Value + "|";
            }
            if (orgID.HasValue)
            {
                cadena += orgID.Value;
            }
            if (!dicPerfilOrganizacionIdioma.ContainsKey(cadena))
            {
                dicPerfilOrganizacionIdioma.Add(cadena, idioma);
            }

            RefrescarCacheResultados_Mensajes(cargadorResultadosHome, pFilaCola, idioma, "recibidos|" + pIdentidadID.ToString() + "|" + usuarioID.ToString(), loggingService, utilidadesVirtuoso);
            RefrescarCacheResultados_Mensajes(cargadorResultadosHome, pFilaCola, idioma, "enviados|" + pIdentidadID.ToString() + "|" + usuarioID.ToString(), loggingService, utilidadesVirtuoso);
            RefrescarCacheResultados_Mensajes(cargadorResultadosHome, pFilaCola, idioma, "eliminados|" + pIdentidadID.ToString() + "|" + usuarioID.ToString(), loggingService, utilidadesVirtuoso);

            RefrescarCacheFacetas_Mensajes(cargadorFacetasHome, pFilaCola, idioma, "recibidos|" + pIdentidadID.ToString() + "|" + usuarioID.ToString(), loggingService, utilidadesVirtuoso);
            RefrescarCacheFacetas_Mensajes(cargadorFacetasHome, pFilaCola, idioma, "enviados|" + pIdentidadID.ToString() + "|" + usuarioID.ToString(), loggingService, utilidadesVirtuoso);
            RefrescarCacheFacetas_Mensajes(cargadorFacetasHome, pFilaCola, idioma, "eliminados|" + pIdentidadID.ToString() + "|" + usuarioID.ToString(), loggingService, utilidadesVirtuoso);

            //Faceta única que se encuentra dentro del mensaje.
            RefrescarCacheFaceta_Mensajes(cargadorFacetasHome, pFilaCola, idioma, 1, "recibidos|" + pIdentidadID.ToString() + "|" + usuarioID.ToString(), "dce:type", loggingService, utilidadesVirtuoso);

            return perfilID;
        }

        private void RefrescarCacheFacetas_Mensajes(CargadorFacetas pCargadorFacetasHome, BaseComunidadDS.ColaRefrescoCacheRow pFilaCola, string idioma, string pParametros_Adiccionales, LoggingService loggingService, UtilidadesVirtuoso utilidadesVirtuoso)
        {
            for (int i = 1; i <= 3; i++)
            {
                RefrescarCacheFaceta_Mensajes(pCargadorFacetasHome, pFilaCola, idioma, i, pParametros_Adiccionales, null, loggingService, utilidadesVirtuoso);
            }
        }

        private void RefrescarCacheFaceta_Mensajes(CargadorFacetas pCargadorFacetasHome, BaseComunidadDS.ColaRefrescoCacheRow pFilaCola, string idioma, int pNumeroFacetas, string pParametros_Adiccionales, string pFaceta, LoggingService loggingService, UtilidadesVirtuoso utilidadesVirtuoso)
        {
            try
            {
                pCargadorFacetasHome.RefrescarFacetas(pFilaCola.ProyectoID, pFilaCola.ProyectoID.Equals(ProyectoAD.MetaProyecto), false, "MyGnoss", false, idioma, (TipoBusqueda)pFilaCola.TipoBusqueda, pNumeroFacetas, pParametros_Adiccionales, false, pFaceta);
            }
            catch (Exception ex)
            {
                try
                {
                    while (!utilidadesVirtuoso.ServidorOperativo(mFicheroConfiguracionBD, mUrlIntragnoss))
                    {
                        Thread.Sleep(3000);
                    }

                    pCargadorFacetasHome.RefrescarFacetas(pFilaCola.ProyectoID, pFilaCola.ProyectoID.Equals(ProyectoAD.MetaProyecto), false, "MyGnoss", false, idioma, (TipoBusqueda)pFilaCola.TipoBusqueda, pNumeroFacetas, pParametros_Adiccionales, false, pFaceta);
                }
                catch (Exception)
                {
                    //Fallo tras el segundo reintento...
                    pFilaCola.Estado = 1;
                    loggingService.GuardarLog("Error al refrescar los resultados la fila " + pFilaCola.ColaID + " ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                }
            }
        }

        private void RefrescarCacheResultados_Mensajes(CargadorResultados pCargadorResultadosHome, BaseComunidadDS.ColaRefrescoCacheRow pFilaCola, string idioma, string pParametros_Adiccionales, LoggingService loggingService, UtilidadesVirtuoso utilidadesVirtuoso)
        {
            try
            {
                pCargadorResultadosHome.RefrescarResultados(pFilaCola.ProyectoID, pFilaCola.ProyectoID.Equals(ProyectoAD.MetaProyecto), true, false, false, idioma, (TipoBusqueda)pFilaCola.TipoBusqueda, 1, false, pParametros_Adiccionales);
            }
            catch (Exception ex)
            {
                try
                {
                    while (!utilidadesVirtuoso.ServidorOperativo(mFicheroConfiguracionBD, mUrlIntragnoss))
                    {
                        Thread.Sleep(3000);
                    }

                    pCargadorResultadosHome.RefrescarResultados(pFilaCola.ProyectoID, pFilaCola.ProyectoID.Equals(ProyectoAD.MetaProyecto), true, false, false, idioma, (TipoBusqueda)pFilaCola.TipoBusqueda, 1, false, pParametros_Adiccionales);
                }
                catch (Exception)
                {
                    //Fallo tras el segundo reintento...
                    pFilaCola.Estado = 1;
                    loggingService.GuardarLog("Error al refrescar los resultados la fila " + pFilaCola.ColaID + " ERROR:  Excepción: " + ex.ToString() + "\n\n\tTraza: " + ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// Actualizamos el contador de num elementos nuevos para cada perfil.
        /// </summary>
        /// <param name="pPerfiles">Lista de perfiles que han recibido un correo.</param>
        private void AgregarNotificacionCorreoNuevoAIdentidades(List<Guid> pPerfiles, EntityContext entityContext, LoggingService loggingService, VirtuosoAD virtuosoAD, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            LiveCN liveCN = new LiveCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
            foreach (Guid perfilID in pPerfiles)
            {
                liveCN.AumentarContadorNuevosMensajes(perfilID);

                EstablecerNumeroMensajesSinLeer(perfilID, entityContext, loggingService, virtuosoAD, servicesUtilVirtuosoAndReplication);
            }
            liveCN.Dispose();
        }

        private void EstablecerNumeroMensajesSinLeer(Guid pPerfilID, EntityContext entityContext, LoggingService loggingService, VirtuosoAD virtuosoAD, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            LiveCN liveCN = new LiveCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);
            FacetadoAD facetadoAD = new FacetadoAD(mFicheroConfiguracionHomeBD, mUrlIntragnoss, loggingService, entityContext, mConfigService, virtuosoAD, servicesUtilVirtuosoAndReplication);
            IdentidadCN identidadCN = new IdentidadCN(entityContext, loggingService, mConfigService, servicesUtilVirtuosoAndReplication);

            Guid? identidadID = identidadCN.ObtenerIdentidadIDDePerfilEnProyecto(ProyectoAD.MetaProyecto, pPerfilID);

            if (identidadID.HasValue)
            {
                Guid usuarioID = identidadCN.ObtenerUsuarioIDConIdentidadID(identidadID.Value);
                int numeroMensajes = facetadoAD.ObtenerNumMensajesPerfilID_Bandeja(usuarioID, identidadID.Value, "Entrada");

                liveCN.ActualizarContadorPerfilMensajesSinLeer(pPerfilID, numeroMensajes);
            }
        }

        /// <summary>
        /// Comprueba si un tag proviene de un filtro
        /// </summary>
        /// <param name="pTags">Cadena que contiene los tags</param>
        /// <param name="pListaTagsFiltros">Lista de tags que provienen de filtros</param>
        /// <param name="pListaTodosTags">Lista de todos los tags</param>
        /// <param name="pDataSet">Data set de la fila de cola</param>
        /// <returns></returns>
        private Dictionary<short, List<string>> ObtenerTagsFiltros(string pTags)
        {
            Dictionary<short, List<string>> listaTagsFiltros = new Dictionary<short, List<string>>();
            //MensajeID
            listaTagsFiltros.Add((short)TiposTags.IDTagMensaje, BuscarTagFiltroEnCadena(ref pTags, Constantes.ID_MENSAJE));

            //Identidad que envia el mensaje
            listaTagsFiltros.Add((short)TiposTags.IDTagMensajeFrom, BuscarTagFiltroEnCadena(ref pTags, Constantes.ID_MENSAJE_FROM));

            //Identidad que recibe el mensaje
            listaTagsFiltros.Add((short)TiposTags.IDTagMensajeTo, BuscarTagFiltroEnCadena(ref pTags, Constantes.IDS_MENSAJE_TO));
            return listaTagsFiltros;
        }

        /// <summary>
        /// Busca un filtro concreto en una cadena
        /// </summary>
        /// <param name="pCadena">Cadena en la que se debe buscar</param>
        /// <param name="pClaveFiltro">Clave del filtro (##CAT_DOC##, ...)</param>
        /// <returns></returns>
        private List<string> BuscarTagFiltroEnCadena(ref string pCadena, string pClaveFiltro)
        {
            string filtro = "";
            List<string> listaFiltros = new List<string>();

            int indiceFiltro = pCadena.IndexOf(pClaveFiltro);

            if (indiceFiltro >= 0)
            {
                string subCadena = pCadena.Substring(indiceFiltro + pClaveFiltro.Length);

                filtro = subCadena.Substring(0, subCadena.IndexOf(pClaveFiltro));

                if ((pClaveFiltro.Equals(Constantes.TIPO_DOC)) || (pClaveFiltro.Equals(Constantes.PERS_U_ORG)) || (pClaveFiltro.Equals(Constantes.ESTADO_COMENTADO)))
                {
                    //Estos tags van con la clave del tag (para tags de tipo entero o similar, ej: Tipos de documento, para que al buscar '0' no aparezcan los tags de todos los recursos que son de tal tipo). 
                    filtro = pClaveFiltro + filtro + pClaveFiltro;
                    pCadena = pCadena.Replace(filtro, "");
                }
                else
                {
                    pCadena = pCadena.Replace(pClaveFiltro + filtro + pClaveFiltro, "");
                    filtro = filtro.ToLower();
                }
                if (filtro.Trim() != "")
                {
                    listaFiltros.Add(filtro);
                }
                listaFiltros.AddRange(BuscarTagFiltroEnCadena(ref pCadena, pClaveFiltro));
            }
            return listaFiltros;
        }

        protected override ControladorServicioGnoss ClonarControlador()
        {
            return new ControladorRefrescoCache(mNumeroMaxPeticionesWebSimultaneas, ScopedFactory, mConfigService);
        }

        #endregion
    }
}
