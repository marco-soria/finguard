// Aspire injects API_HTTPS / API_HTTP from .WithReference(api) in the AppHost.
const target = process.env.API_HTTPS || process.env.API_HTTP;

if (!target) {
  throw new Error('API endpoint is not configured. Run the app through Aspire (aspire run).');
}

module.exports = {
  '/api': { target, secure: false, changeOrigin: true },
  // Development sign-in (POST /dev/token) stays same-origin like /api.
  '/dev': { target, secure: false, changeOrigin: true },
};
