export const environment = {
  production: true,
  // Feature services append `/api/...` themselves. Keep the base empty so
  // production requests stay same-origin and reach nginx's `/api` proxy once.
  apiUrl: '',
  ai: {
    provider: 'Mock',
    defaultVoice: 'en-US-Neural',
    defaultLanguage: 'en-US',
  },
};
