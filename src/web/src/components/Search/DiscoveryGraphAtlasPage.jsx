import DiscoveryGraphAtlasPanel from './DiscoveryGraphAtlasPanel';
import React from 'react';

const DiscoveryGraphAtlasPage = ({ server }) => (
  <div className="view">
    <DiscoveryGraphAtlasPanel
      disabled={!server?.isConnected}
      persistRoute
    />
  </div>
);

export default DiscoveryGraphAtlasPage;
