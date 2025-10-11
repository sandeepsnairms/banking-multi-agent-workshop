// environment.ts

// Detect if we're running in GitHub Codespaces by checking the hostname pattern
function getApiUrl(): string {
  const hostname = window.location.hostname;
  
  // If hostname contains the Codespaces pattern, construct the API URL
  if (hostname.includes('.app.github.dev')) {
    // Extract the codespace name from the hostname (e.g., "codespace-name-4200.app.github.dev")
    const parts = hostname.split('-');
    if (parts.length >= 3) {
      // Remove the port number and reconstruct with API port
      const codespaceName = parts.slice(0, -1).join('-');
      return `https://${codespaceName}-63280.app.github.dev/`;
    }
  }
  
  // Default to localhost for local development
  return 'http://localhost:63280/';
}

export const environment = {
  production: false,
  apiUrl: getApiUrl(),
};
