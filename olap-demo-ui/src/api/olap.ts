import axios from 'axios'
import type { OlapRequest, SliceDiceRequest, OlapResult, MetadataResult } from '@/types/olap'

const api = axios.create({ baseURL: '/api/olap' })

export const olapApi = {
  getMetadata: (cube?: string): Promise<MetadataResult> =>
    api.get('/metadata', { params: { cube } }).then(r => r.data),

  getDefaultQuery: (cube?: string): Promise<OlapResult> =>
    api.get('/default-query', { params: { cube } }).then(r => r.data),

  drill: (req: OlapRequest): Promise<OlapResult> =>
    api.post('/drill', req).then(r => r.data),

  rollup: (req: OlapRequest): Promise<OlapResult> =>
    api.post('/rollup', req).then(r => r.data),

  sliceDice: (req: SliceDiceRequest): Promise<OlapResult> =>
    api.post('/slice-dice', req).then(r => r.data),

  pivot: (req: OlapRequest): Promise<OlapResult> =>
    api.post('/pivot', req).then(r => r.data),
}
