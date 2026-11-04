/* Copyright (c) 2026 Balazs Bodnar. SPDX-License-Identifier: MIT */
package dydo.statechart;

import java.io.IOException;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.rmi.server.RMISocketFactory;

/** Statechart's local RMI transport; not a sandbox for model extensions. */
public final class LoopbackSockets extends RMISocketFactory {
    private static final String ADDRESS = "127.0.0.1";

    @Override
    public ServerSocket createServerSocket(int port) throws IOException {
        return new ServerSocket(port, 50, InetAddress.getByName(ADDRESS));
    }

    @Override
    public Socket createSocket(String host, int port) throws IOException {
        if (!ADDRESS.equals(host) && !"localhost".equalsIgnoreCase(host)) {
            throw new IOException("Statechart RMI rejects a nonlocal endpoint: " + host);
        }
        // Resolve only our numeric loopback constant, never an endpoint-supplied hostname.
        return new Socket(InetAddress.getByName(ADDRESS), port);
    }
}
