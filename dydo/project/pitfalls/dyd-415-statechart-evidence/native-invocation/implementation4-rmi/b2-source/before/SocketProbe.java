/* Copyright (c) 2026 Balazs Bodnar. SPDX-License-Identifier: MIT */
package dydo.statechart.proof;

import dydo.statechart.LoopbackSockets;
import dydo.statechart.LoopbackTlc;
import java.io.IOException;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.ByteOrder;
import java.nio.file.DirectoryStream;
import java.nio.file.NoSuchFileException;
import java.rmi.server.RemoteObject;
import java.rmi.server.RMISocketFactory;
import java.rmi.server.UnicastRemoteObject;
import java.util.Arrays;
import java.util.HashSet;
import java.util.Set;

/** Isolated POSIX fixture only. No fixture class belongs in the product jar. */
public final class SocketProbe {
    private SocketProbe() { }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static boolean ipv4Loopback(String hex) {
        byte[] bytes = new byte[hex.length() / 2];
        require(bytes.length == 4 || bytes.length == 16, "Unknown Linux socket address width");
        for (int word = 0; word < bytes.length; word += 4) {
            for (int index = 0; index < 4; index++) {
                int offset = word + (ByteOrder.nativeOrder() == ByteOrder.LITTLE_ENDIAN ? 3 - index : index);
                bytes[word + index] = (byte) Integer.parseInt(hex.substring(offset * 2, offset * 2 + 2), 16);
            }
        }
        byte[] expected = bytes.length == 4
            ? new byte[] { 127, 0, 0, 1 }
            : new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1, -1, 127, 0, 0, 1 };
        return Arrays.equals(bytes, expected);
    }

    private static Set<String> socketInodes() throws IOException {
        Set<String> inodes = new HashSet<>();
        System.out.println("PROCESS " + ProcessHandle.current().pid());
        System.out.println("PROC_STAT " + Files.readString(Path.of("/proc/self/stat")));
        try (DirectoryStream<Path> descriptors = Files.newDirectoryStream(Path.of("/proc/self/fd"))) {
            for (Path descriptor : descriptors) {
                String target;
                try {
                    target = Files.readSymbolicLink(descriptor).toString();
                } catch (NoSuchFileException closedDescriptor) {
                    System.out.println("FD_CLOSED_DURING_OBSERVATION " + descriptor);
                    continue;
                }
                System.out.println("FD " + descriptor + " " + target);
                if (target.startsWith("socket:[") && target.endsWith("]")) {
                    inodes.add(target.substring(8, target.length() - 1));
                }
            }
        }
        return inodes;
    }

    private static void table(int expectedPort) throws IOException {
        Set<String> inodes = socketInodes();
        int observed = 0;
        for (String name : new String[] { "tcp", "tcp6" }) {
            for (String row : Files.readAllLines(Path.of("/proc/net", name))) {
                System.out.println(name.toUpperCase() + " " + row);
                String[] fields = row.trim().split("\\s+");
                if (fields.length < 10 || !"0A".equals(fields[3])) continue;
                String[] endpoint = fields[1].split(":");
                int port = Integer.parseInt(endpoint[1], 16);
                boolean owned = inodes.contains(fields[9]);
                System.out.println("LISTEN " + name + " " + fields[1] + " inode=" + fields[9] + " owned=" + owned);
                require(ipv4Loopback(endpoint[0]), "Wildcard/nonlocal listener observed: " + fields[1]);
                if (owned && (expectedPort == -1 || port == expectedPort)) observed++;
            }
        }
        require(observed > 0, "No process-owned loopback listener observed; missing evidence is not proof");
    }

    private static void bound() throws IOException {
        LoopbackSockets factory = new LoopbackSockets();
        try (ServerSocket server = factory.createServerSocket(0)) {
            require("127.0.0.1".equals(server.getInetAddress().getHostAddress()), "Wildcard or nonlocal listener");
            System.out.println("BOUND " + server.getInetAddress().getHostAddress() + ":" + server.getLocalPort());
            table(server.getLocalPort());
            try (Socket numeric = factory.createSocket("127.0.0.1", server.getLocalPort());
                 Socket acceptedNumeric = server.accept();
                 Socket named = factory.createSocket("localhost", server.getLocalPort());
                 Socket acceptedNamed = server.accept()) {
                require(numeric.isConnected() && named.isConnected(), "Local clients failed");
                require(acceptedNumeric.getInetAddress().isLoopbackAddress()
                    && acceptedNamed.getInetAddress().isLoopbackAddress(), "Nonlocal peer");
            }
        }
    }

    private static void rmi() throws Exception {
        System.setProperty("java.rmi.server.hostname", "127.0.0.1");
        RMISocketFactory.setSocketFactory(new LoopbackSockets());
        EchoServer server = new EchoServer();
        try {
            Echo stub = (Echo) RemoteObject.toStub(server);
            require("statechart-local-rmi".equals(stub.echo("statechart-local-rmi")), "Local RMI round trip failed");
            System.out.println("RMI_LOCAL_OK " + stub);
            table(-1);
        } finally {
            require(UnicastRemoteObject.unexportObject(server, true), "Owned RMI object was not unexported");
        }
    }

    public static void main(String[] arguments) throws Exception {
        switch (arguments[0]) {
            case "bound": bound(); break;
            case "rmi": rmi(); break;
            case "nonlocal":
                // Must throw before DNS or connection. Success is a failing fixture.
                new LoopbackSockets().createSocket("192.0.2.1", 9).close();
                throw new AssertionError("Nonlocal endpoint was accepted");
            case "bind-failure":
                try (ServerSocket held = new LoopbackSockets().createServerSocket(0)) {
                    new LoopbackSockets().createServerSocket(held.getLocalPort()).close();
                    throw new AssertionError("Occupied loopback port was silently accepted");
                }
            case "existing-factory":
                RMISocketFactory.setSocketFactory(new LoopbackSockets());
                LoopbackTlc.main(new String[] { "TLC_MUST_NOT_START" });
                throw new AssertionError("Existing factory was replaced");
            case "repeat-install":
                RMISocketFactory.setSocketFactory(new LoopbackSockets());
                RMISocketFactory.setSocketFactory(new LoopbackSockets());
                throw new AssertionError("Second global factory was accepted");
            default: throw new IllegalArgumentException("Unknown finite fixture mode");
        }
    }
}
