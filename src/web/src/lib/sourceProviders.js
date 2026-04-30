import api from './api';

export const getSourceProviders = async () => {
  const response = (await api.get('/source-providers')).data;

  return {
    acquisitionPlanningEnabled:
      response?.acquisitionPlanningEnabled ??
      response?.AcquisitionPlanningEnabled ??
      false,
    profilePolicies:
      response?.profilePolicies ?? response?.ProfilePolicies ?? [],
    providers:
      response?.providers ?? response?.Providers ?? [],
  };
};
