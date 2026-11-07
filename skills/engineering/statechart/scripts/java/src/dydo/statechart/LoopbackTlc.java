/* Copyright (c) 2026 Balazs Bodnar. SPDX-License-Identifier: MIT */
package dydo.statechart;

import java.io.IOException;
import java.rmi.server.RMISocketFactory;

/** Installs local transport before entering the unchanged pinned TLC checker. */
public final class LoopbackTlc {
    private LoopbackTlc() { }

    public static void main(String[] arguments) throws Exception {
        if (RMISocketFactory.getSocketFactory() != null) {
            throw new IOException("Statechart cannot replace an existing RMI socket factory.");
        }
        System.setProperty("java.rmi.server.hostname", "127.0.0.1");
        RMISocketFactory.setSocketFactory(new LoopbackSockets());
        tlc2.TLC.main(arguments);
    }
}
