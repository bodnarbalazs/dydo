import { createContext, useContext } from 'react';

export interface MapActions {
  togglePlate: (id: string) => void;
}

export const MapActionsContext = createContext<MapActions>({ togglePlate: () => undefined });

export function useMapActions(): MapActions {
  return useContext(MapActionsContext);
}
