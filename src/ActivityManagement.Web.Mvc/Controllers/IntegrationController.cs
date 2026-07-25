using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.ServiceRequests;
using ActivityManagement.ServiceRequests.Dto;
using ActivityManagement.SystemSettings;

namespace ActivityManagement.Web.Controllers
{
    // FAZ 2 — Portal entegrasyonu (webhook alıcısı). psm.tdv.org / destek.cmit.com.tr yeni/güncel talebi
    // buraya token'lı POST atar. Idempotent: (Source, ExternalRef) ile upsert.
    //
    // Anahtar (InboundApiKey) admin panelinden yönetilir (Admin → Entegrasyon). Tanımlı DEĞİLSE endpoint 503.
    // Cookie yerine X-Api-Key header ile doğrulanır.
    [AllowAnonymous]
    [Route("api/integration")]
    public class IntegrationController : ActivityManagementControllerBase
    {
        private readonly IServiceRequestAppService _requestAppService;
        private readonly IIntegrationSettingsAppService _settingsAppService;

        public IntegrationController(IServiceRequestAppService requestAppService, IIntegrationSettingsAppService settingsAppService)
        {
            _requestAppService = requestAppService;
            _settingsAppService = settingsAppService;
        }

        [HttpPost("requests")]
        public async Task<IActionResult> Requests([FromBody] PortalRequestDto input)
        {
            var (enabled, key) = await _settingsAppService.GetInboundAsync();
            if (!enabled)
                return StatusCode(503, new { error = "Talep entegrasyonu henüz etkin değil." });

            var provided = Request.Headers["X-Api-Key"].ToString();
            if (!string.Equals(provided, key, StringComparison.Ordinal))
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

        [HttpGet("ping")]
        public async Task<IActionResult> Ping()
        {
            var (enabled, _) = await _settingsAppService.GetInboundAsync();
            return Ok(new { ok = true, enabled });
        }
    }
}
