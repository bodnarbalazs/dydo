import ELK from 'elkjs/lib/elk-api';
import elkWorkerUrl from 'elkjs/lib/elk-worker.min.js?url';
import type { ELK as Elk } from 'elkjs/lib/elk-api';
import type { MapModel } from '../graph/mapModel';
import { toFlow, type MapFlow } from './toFlow';

/** ELK runs in a Web Worker so a large layout never blocks the page. */
export function createElk(): Elk {
  return new ELK({ workerFactory: () => new Worker(elkWorkerUrl) });
}

export async function layoutMap(elk: Elk, model: MapModel): Promise<MapFlow> {
  return toFlow(model, await elk.layout(model.elk));
}
