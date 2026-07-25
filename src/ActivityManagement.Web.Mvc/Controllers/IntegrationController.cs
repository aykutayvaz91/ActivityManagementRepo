using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ActivityManagement.ServiceRequests;
using ActivityManagement.ServiceRequests.Dto;

namespace ActivityManagement.Web.Controllers
{
    // FAZ 2 — Portal entegrasyonu (webhook alıcısı). psm.tdv.org / destek.cmit.com.tr yeni/güncel talebi
    // buraya token'lı POST atar. Idempotent: (Source, ExternalRef) ile upsert.
    //
    // Etkinleştirme: appsettings(.Production).json → "Integration": { "ApiKey": "<gizli-anahtar>" }.
    // Anahtar tanımlı DEĞİLSE endpoint 503 döner (kapalı). Cookie yerine X-Api-Key header ile doğrulanır.
    [AllowAnonymous]
    [Route("api/integration")]
    public class IntegrationController : ActivityManagementControllerBase
    {
        private readonly IServiceRequestAppService _requestAppService;
        private readonly IConfiguration _config;

        public IntegrationController(IServiceRequestAppService requestAppService, IConfiguration config)
        {
            _requestAppService = requestAppService;
            _config = config;
        }

        // Tek talebi al/güncelle. Portal her talep için bir POST atar.
        [HttpPost("requests")]
        public async Task<IActionResult> Requests([FromBody] PortalRequestDto input)
        {
            var configured = _config["Integration:ApiKey"];
            if (string.IsNullOrWhiteSpace(configured))
                return StatusCode(503, new { error = "Talep entegrasyonu henüz etkin değil." });

            var provided = Request.Headers["X-Api-Key"].ToString();
            if (!string.Equals(provided, configured, StringComparison.Ordinal))
                return Unauthorized(new { error = "Geçersiz API anahtarı." });

            if (input == null || string.IsNullOrWhiteSpace(input.Title))
                return BadRequest(new { error = "Geçersiz talep verisi (başlık zorunlu)." });

            try
            {
                var id = await _requestAppService.UpsertFromPortalAsync(input);
                return Ok(new { id, status = "ok" });
            }
            catch (Abp.UI.UserFriendlyException ex) { return BadRequest(new { error = ex.Message }); }
            catch (Exception ex)
            {
                ActivityManagement.Logging.ErrorLog.Write(ex, "Integration/Requests");
                return StatusCode(500, new { error = "Talep işlenemedi." });
            }
        }

        // Basit sağlık ucu (entegrasyonun ayakta olduğunu doğrulamak için).
        [HttpGet("ping")]
        public IActionResult Ping() => Ok(new { ok = true, enabled = !string.IsNullOrWhiteSpace(_config["Integration:ApiKey"]) });
    }
}
